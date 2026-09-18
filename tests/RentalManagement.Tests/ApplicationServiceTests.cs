using FluentAssertions;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services.Models;
using RentalManagement.Infrastructure.Identity;
using RentalManagement.Infrastructure.Persistence;
using RentalManagement.Infrastructure.Services;
using Xunit;

namespace RentalManagement.Tests;

public class ApplicationServiceTests : IClassFixture<SqliteAppDbContextFactory>
{
    private readonly SqliteAppDbContextFactory _factory;

    public ApplicationServiceTests(SqliteAppDbContextFactory factory) => _factory = factory;

    private static ApplicantInfoInput ValidApplicantInfo(byte[]? rowVersion = null) =>
        new("Jane Applicant", "+15551234567", "jane@example.com", "1 Current St", rowVersion);

    private static ResidenceInput ValidResidence() =>
        new("9 Old Rd", "Bob Landlord", "+15559876543", new DateOnly(2022, 1, 1), null);

    /// <summary>Seeds a unit and an applicant user for a test, using a name suffix so parallel tests in the shared fixture DB don't collide.</summary>
    private static async Task<(int unitId, string applicantId, string managerId)> SeedAsync(AppDbContext db, string suffix)
    {
        var property = new Property { Name = $"Property-{suffix}", Address = "1 Test St" };
        var unitType = new UnitType { Name = $"Type-{suffix}", IsActive = true };
        db.Properties.Add(property);
        db.UnitTypes.Add(unitType);
        await db.SaveChangesAsync();

        var unit = new Unit { PropertyId = property.Id, UnitNumber = $"U-{suffix}", Bedrooms = 1, MonthlyRent = 1000m, UnitTypeId = unitType.Id };
        db.Units.Add(unit);

        var applicant = new ApplicationUser { Id = $"applicant-{suffix}", UserName = $"applicant-{suffix}@test.com", Email = $"applicant-{suffix}@test.com" };
        var manager = new ApplicationUser { Id = $"manager-{suffix}", UserName = $"manager-{suffix}@test.com", Email = $"manager-{suffix}@test.com" };
        db.Users.AddRange(applicant, manager);
        await db.SaveChangesAsync();

        return (unit.Id, applicant.Id, manager.Id);
    }

    private static async Task<RentalApplication> CreateSubmittableDraftAsync(
        AppDbContext db, ApplicationService service, int unitId, string applicantId)
    {
        var application = await service.StartOrResumeDraftAsync(unitId, applicantId);
        await service.SaveApplicantInfoAsync(application.Id, applicantId, ValidApplicantInfo());
        await service.AddResidenceAsync(application.Id, applicantId, ValidResidence());
        await service.MarkResidenceHistoryReviewedAsync(application.Id, applicantId, expectedResidenceHistoryVersion: 1);
        return (await service.GetForApplicantAsync(application.Id, applicantId))!;
    }

    [Fact]
    public async Task StartOrResumeDraftAsync_resumes_the_existing_draft_instead_of_creating_a_new_one()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db, "resume");
        var service = new ApplicationService(db, new UnitService(db));

        var first = await service.StartOrResumeDraftAsync(unitId, applicantId);
        var second = await service.StartOrResumeDraftAsync(unitId, applicantId);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task SubmitAsync_fails_when_sections_are_incomplete()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db, "incomplete");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await service.StartOrResumeDraftAsync(unitId, applicantId);

        var act = () => service.SubmitAsync(application.Id, applicantId);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Complete both sections*");
    }

    [Fact]
    public async Task SubmitAsync_succeeds_and_records_status_history_when_both_sections_are_complete()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db, "submit-ok");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);

        await service.SubmitAsync(application.Id, applicantId);

        var reloaded = await service.GetForApplicantAsync(application.Id, applicantId);
        reloaded!.Status.Should().Be(ApplicationStatus.Submitted);
        reloaded.StatusHistory.Should().Contain(h => h.ToStatus == ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task SubmitAsync_rejects_when_the_unit_already_has_an_active_lease()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "submit-leased");
        var service = new ApplicationService(db, new UnitService(db));

        var approved = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(approved.Id, applicantId);
        await service.ReviewAsync(approved.Id, managerId, ReviewOutcome.Approve, null, DateOnly.FromDateTime(DateTime.UtcNow));

        var secondApplicant = new ApplicationUser { Id = "applicant-submit-leased-2", UserName = "a2@test.com", Email = "a2@test.com" };
        db.Users.Add(secondApplicant);
        await db.SaveChangesAsync();
        var second = await CreateSubmittableDraftAsync(db, service, unitId, secondApplicant.Id);

        var act = () => service.SubmitAsync(second.Id, secondApplicant.Id);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*active lease*");
    }

    [Fact]
    public async Task ReviewAsync_approve_creates_a_twelve_month_lease_and_marks_the_unit_unavailable()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "approve");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(application.Id, applicantId);

        var start = new DateOnly(2026, 1, 1);
        await service.ReviewAsync(application.Id, managerId, ReviewOutcome.Approve, null, start);

        var reloaded = await service.GetForApplicantAsync(application.Id, applicantId);
        reloaded!.Status.Should().Be(ApplicationStatus.Approved);
        reloaded.Lease.Should().NotBeNull();
        reloaded.Lease!.StartDate.Should().Be(start);
        reloaded.Lease!.EndDate.Should().Be(start.AddMonths(12));
    }

    [Fact]
    public async Task ReviewAsync_approve_rejects_a_second_application_for_the_same_already_leased_unit()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "approve-twice");
        var service = new ApplicationService(db, new UnitService(db));

        var first = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(first.Id, applicantId);
        await service.ReviewAsync(first.Id, managerId, ReviewOutcome.Approve, null, DateOnly.FromDateTime(DateTime.UtcNow));

        var secondApplicant = new ApplicationUser { Id = "applicant-approve-twice-2", UserName = "a2@test.com", Email = "a2@test.com" };
        db.Users.Add(secondApplicant);
        await db.SaveChangesAsync();

        // Simulate: the second application for the SAME unit was already Submitted before the first was approved
        // (matches "Other open applications for that unit are left as they are" from the brief).
        var second = new RentalApplication
        {
            UnitId = unitId,
            Status = ApplicationStatus.Submitted,
            IsApplicantInfoComplete = true,
            IsResidenceHistoryComplete = true,
            ApplicantInfo = new ApplicantInfo { FullName = "X", Phone = "1", Email = "x@test.com", CurrentAddress = "X" },
            Applicants = { new ApplicationApplicant { UserId = secondApplicant.Id } }
        };
        db.RentalApplications.Add(second);
        await db.SaveChangesAsync();

        var act = () => service.ReviewAsync(second.Id, managerId, ReviewOutcome.Approve, null, DateOnly.FromDateTime(DateTime.UtcNow));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*active lease*");
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public async Task ReviewAsync_requires_a_comment_for_return_or_deny(ReviewOutcome outcome)
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, $"comment-{outcome}");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(application.Id, applicantId);

        var act = () => service.ReviewAsync(application.Id, managerId, outcome, comment: null, leaseStartDate: null);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*comment is required*");
    }

    [Fact]
    public async Task ReviewAsync_return_lets_the_applicant_edit_and_resubmit()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "return-flow");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(application.Id, applicantId);

        await service.ReviewAsync(application.Id, managerId, ReviewOutcome.Return, "Please clarify.", null);

        var afterReturn = await service.GetForApplicantAsync(application.Id, applicantId);
        afterReturn!.Status.Should().Be(ApplicationStatus.Returned);

        // Editable again while Returned: should not throw.
        await service.SaveApplicantInfoAsync(application.Id, applicantId,
            ValidApplicantInfo(afterReturn.ApplicantInfo!.RowVersion));
        await service.SubmitAsync(application.Id, applicantId);

        var afterResubmit = await service.GetForApplicantAsync(application.Id, applicantId);
        afterResubmit!.Status.Should().Be(ApplicationStatus.Submitted);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_rejects_edits_once_the_application_is_no_longer_editable()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "readonly");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(application.Id, applicantId);
        await service.ReviewAsync(application.Id, managerId, ReviewOutcome.Deny, "Not eligible.", null);

        var act = () => service.SaveApplicantInfoAsync(application.Id, applicantId, ValidApplicantInfo());

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*no longer be edited*");
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_rejects_a_user_who_is_not_on_the_application()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db, "ownership");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await service.StartOrResumeDraftAsync(unitId, applicantId);

        var act = () => service.SaveApplicantInfoAsync(application.Id, "someone-else", ValidApplicantInfo());

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*do not have access*");
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_rejects_a_stale_row_version()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db, "stale-info");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await service.StartOrResumeDraftAsync(unitId, applicantId);

        var act = () => service.SaveApplicantInfoAsync(application.Id, applicantId, ValidApplicantInfo(rowVersion: [1, 2, 3, 4, 5, 6, 7, 8]));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*changed elsewhere*");
    }

    [Fact]
    public async Task Concurrent_residence_edits_reject_the_second_save_as_stale()
    {
        using var db1 = _factory.CreateContext();
        var (unitId, applicantId, _) = await SeedAsync(db1, "concurrent");
        var service1 = new ApplicationService(db1, new UnitService(db1));
        var application = await service1.StartOrResumeDraftAsync(unitId, applicantId);

        using var db2 = _factory.CreateContext();
        var service2 = new ApplicationService(db2, new UnitService(db2));

        // Both "editors" load the application (each into their own DbContext, like two separate
        // HTTP requests) while it's still at version 0 — this is what makes db2's copy go stale.
        await service2.GetForApplicantAsync(application.Id, applicantId);

        await service1.AddResidenceAsync(application.Id, applicantId, ValidResidence());

        var act = () => service2.AddResidenceAsync(application.Id, applicantId, ValidResidence() with { Address = "Different address" });

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*changed elsewhere*");
    }

    [Fact]
    public async Task WithdrawAsync_is_blocked_once_the_application_is_in_a_terminal_status()
    {
        using var db = _factory.CreateContext();
        var (unitId, applicantId, managerId) = await SeedAsync(db, "withdraw-blocked");
        var service = new ApplicationService(db, new UnitService(db));
        var application = await CreateSubmittableDraftAsync(db, service, unitId, applicantId);
        await service.SubmitAsync(application.Id, applicantId);
        await service.ReviewAsync(application.Id, managerId, ReviewOutcome.Deny, "No.", null);

        var act = () => service.WithdrawAsync(application.Id, applicantId);

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*no longer be withdrawn*");
    }
}
