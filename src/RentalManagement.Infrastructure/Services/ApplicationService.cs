using Microsoft.EntityFrameworkCore;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services;
using RentalManagement.Domain.Services.Models;
using RentalManagement.Infrastructure.Persistence;

namespace RentalManagement.Infrastructure.Services;

public class ApplicationService(AppDbContext db, IUnitService unitService) : IApplicationService
{
    public async Task<RentalApplication> StartOrResumeDraftAsync(int unitId, string applicantUserId)
    {
        var existingDraft = await db.RentalApplications
            .Include(a => a.ApplicantInfo)
            .FirstOrDefaultAsync(a => a.UnitId == unitId
                && a.Status == ApplicationStatus.Draft
                && a.Applicants.Any(x => x.UserId == applicantUserId));

        if (existingDraft is not null)
        {
            return existingDraft;
        }

        var application = new RentalApplication
        {
            UnitId = unitId,
            Status = ApplicationStatus.Draft,
            ApplicantInfo = new ApplicantInfo(),
            Applicants = { new ApplicationApplicant { UserId = applicantUserId } }
        };

        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();
        return application;
    }

    public Task<RentalApplication?> GetForApplicantAsync(int applicationId, string userId) =>
        FullQuery().FirstOrDefaultAsync(a => a.Id == applicationId && a.Applicants.Any(x => x.UserId == userId));

    public Task<RentalApplication?> GetForManagerAsync(int applicationId) =>
        FullQuery().FirstOrDefaultAsync(a => a.Id == applicationId);

    public async Task SaveApplicantInfoAsync(int applicationId, string userId, ApplicantInfoInput input)
    {
        var application = await LoadEditableOwnedAsync(applicationId, userId, includeApplicantInfo: true);
        var info = application.ApplicantInfo!;

        db.Entry(info).Property(x => x.RowVersion).OriginalValue = input.ExpectedRowVersion;

        info.FullName = input.FullName;
        info.Phone = input.Phone;
        info.Email = input.Email;
        info.CurrentAddress = input.CurrentAddress;
        application.IsApplicantInfoComplete = true;

        await SaveWithConcurrencyCheckAsync();
    }

    public async Task MarkResidenceHistoryReviewedAsync(int applicationId, string userId, int expectedResidenceHistoryVersion)
    {
        var application = await LoadEditableOwnedAsync(applicationId, userId, includeApplicantInfo: false);
        EnsureResidenceHistoryVersionMatches(application, expectedResidenceHistoryVersion);

        application.IsResidenceHistoryComplete = true;
        await db.SaveChangesAsync();
    }

    public async Task<ResidenceHistoryEntry> AddResidenceAsync(int applicationId, string userId, ResidenceInput input)
    {
        var application = await LoadEditableOwnedAsync(applicationId, userId, includeApplicantInfo: false, includeResidenceHistory: true);

        var entry = new ResidenceHistoryEntry
        {
            RentalApplicationId = applicationId,
            Address = input.Address,
            LandlordName = input.LandlordName,
            LandlordPhone = input.LandlordPhone,
            MoveInDate = input.MoveInDate,
            MoveOutDate = input.MoveOutDate
        };
        db.ResidenceHistoryEntries.Add(entry);
        application.ResidenceHistoryVersion++;

        await SaveWithConcurrencyCheckAsync();
        return entry;
    }

    public async Task UpdateResidenceAsync(int applicationId, string userId, int entryId, ResidenceInput input)
    {
        var application = await LoadEditableOwnedAsync(applicationId, userId, includeApplicantInfo: false, includeResidenceHistory: true);
        var entry = application.ResidenceHistory.FirstOrDefault(r => r.Id == entryId)
            ?? throw new DomainValidationException("Residence entry not found.");

        entry.Address = input.Address;
        entry.LandlordName = input.LandlordName;
        entry.LandlordPhone = input.LandlordPhone;
        entry.MoveInDate = input.MoveInDate;
        entry.MoveOutDate = input.MoveOutDate;
        application.ResidenceHistoryVersion++;

        await SaveWithConcurrencyCheckAsync();
    }

    public async Task RemoveResidenceAsync(int applicationId, string userId, int entryId)
    {
        var application = await LoadEditableOwnedAsync(applicationId, userId, includeApplicantInfo: false, includeResidenceHistory: true);
        var entry = application.ResidenceHistory.FirstOrDefault(r => r.Id == entryId)
            ?? throw new DomainValidationException("Residence entry not found.");

        db.ResidenceHistoryEntries.Remove(entry);
        application.ResidenceHistoryVersion++;

        await SaveWithConcurrencyCheckAsync();
    }

    public async Task SubmitAsync(int applicationId, string userId)
    {
        var application = await db.RentalApplications
            .Include(a => a.Applicants)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new DomainValidationException("Application not found.");

        EnsureOwnedByApplicant(application, userId);
        EnsureEditable(application);

        if (!application.IsApplicantInfoComplete || !application.IsResidenceHistoryComplete)
        {
            throw new DomainValidationException("Complete both sections before submitting.");
        }

        if (!await unitService.IsAvailableAsync(application.UnitId, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            throw new DomainValidationException("This unit already has an active lease and cannot accept new submissions.");
        }

        TransitionStatus(application, ApplicationStatus.Submitted, userId, comment: null);
        application.SubmittedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    public async Task WithdrawAsync(int applicationId, string userId)
    {
        var application = await db.RentalApplications
            .Include(a => a.Applicants)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new DomainValidationException("Application not found.");

        EnsureOwnedByApplicant(application, userId);

        if (application.Status is ApplicationStatus.Approved or ApplicationStatus.Denied or ApplicationStatus.Withdrawn)
        {
            throw new DomainValidationException("This application can no longer be withdrawn.");
        }

        TransitionStatus(application, ApplicationStatus.Withdrawn, userId, comment: null);
        await db.SaveChangesAsync();
    }

    public async Task ReviewAsync(int applicationId, string managerId, ReviewOutcome outcome, string? comment, DateOnly? leaseStartDate)
    {
        var application = await db.RentalApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new DomainValidationException("Application not found.");

        if (application.Status != ApplicationStatus.Submitted)
        {
            throw new DomainValidationException("Only submitted applications can be reviewed.");
        }

        if (outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(comment))
        {
            throw new DomainValidationException("A comment is required to return or deny an application.", nameof(comment));
        }

        switch (outcome)
        {
            case ReviewOutcome.Approve:
                if (!await unitService.IsAvailableAsync(application.UnitId, DateOnly.FromDateTime(DateTime.UtcNow)))
                {
                    throw new DomainValidationException("This unit already has an active lease.");
                }
                if (leaseStartDate is null)
                {
                    throw new DomainValidationException("A lease start date is required.", nameof(leaseStartDate));
                }

                db.Leases.Add(new Lease
                {
                    UnitId = application.UnitId,
                    RentalApplicationId = application.Id,
                    StartDate = leaseStartDate.Value,
                    EndDate = leaseStartDate.Value.AddMonths(12)
                });
                TransitionStatus(application, ApplicationStatus.Approved, managerId, comment);
                break;

            case ReviewOutcome.Return:
                TransitionStatus(application, ApplicationStatus.Returned, managerId, comment);
                break;

            case ReviewOutcome.Deny:
                TransitionStatus(application, ApplicationStatus.Denied, managerId, comment);
                break;
        }

        await db.SaveChangesAsync();
    }

    public async Task<PagedResult<RentalApplication>> GetListAsync(ApplicationListQuery query)
    {
        var applications = db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ApplicantInfo)
            .AsQueryable();

        if (query.Status is not null)
        {
            applications = applications.Where(a => a.Status == query.Status);
        }

        if (query.PropertyId is not null)
        {
            applications = applications.Where(a => a.Unit.PropertyId == query.PropertyId);
        }

        if (query.ApplicantUserId is not null)
        {
            applications = applications.Where(a => a.Applicants.Any(x => x.UserId == query.ApplicantUserId));
        }

        var totalCount = await applications.CountAsync();
        var items = await applications
            .OrderByDescending(a => a.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<RentalApplication>(items, totalCount);
    }

    public Task<Dictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds) =>
        db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

    private IQueryable<RentalApplication> FullQuery() =>
        db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Unit).ThenInclude(u => u.UnitType)
            .Include(a => a.ApplicantInfo)
            .Include(a => a.ResidenceHistory)
            .Include(a => a.Applicants)
            .Include(a => a.StatusHistory)
            .Include(a => a.Lease);

    private async Task<RentalApplication> LoadEditableOwnedAsync(
        int applicationId, string userId, bool includeApplicantInfo, bool includeResidenceHistory = false)
    {
        IQueryable<RentalApplication> queryable = db.RentalApplications.Include(a => a.Applicants);
        if (includeApplicantInfo)
        {
            queryable = queryable.Include(a => a.ApplicantInfo);
        }
        if (includeResidenceHistory)
        {
            queryable = queryable.Include(a => a.ResidenceHistory);
        }

        var application = await queryable.FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new DomainValidationException("Application not found.");

        EnsureOwnedByApplicant(application, userId);
        EnsureEditable(application);
        return application;
    }

    private static void EnsureOwnedByApplicant(RentalApplication application, string userId)
    {
        if (!application.Applicants.Any(x => x.UserId == userId))
        {
            throw new DomainValidationException("You do not have access to this application.");
        }
    }

    private static void EnsureEditable(RentalApplication application)
    {
        if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
        {
            throw new DomainValidationException("This application can no longer be edited.");
        }
    }

    private static void EnsureResidenceHistoryVersionMatches(RentalApplication application, int expectedVersion)
    {
        if (application.ResidenceHistoryVersion != expectedVersion)
        {
            throw new DomainValidationException("The residence history has changed. Reload and try again.");
        }
    }

    private void TransitionStatus(RentalApplication application, ApplicationStatus to, string changedByUserId, string? comment)
    {
        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = application.Status,
            ToStatus = to,
            ChangedByUserId = changedByUserId,
            Comment = comment
        });
        application.Status = to;
    }

    private async Task SaveWithConcurrencyCheckAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainValidationException("This section was changed elsewhere. Reload and try again.");
        }
    }
}
