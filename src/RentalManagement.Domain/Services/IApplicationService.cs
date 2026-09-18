using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Services.Models;

namespace RentalManagement.Domain.Services;

public interface IApplicationService
{
    Task<RentalApplication> StartOrResumeDraftAsync(int unitId, string applicantUserId);

    Task<RentalApplication?> GetForApplicantAsync(int applicationId, string userId);
    Task<RentalApplication?> GetForManagerAsync(int applicationId);

    Task SaveApplicantInfoAsync(int applicationId, string userId, ApplicantInfoInput input);
    Task MarkResidenceHistoryReviewedAsync(int applicationId, string userId, int expectedResidenceHistoryVersion);

    Task<ResidenceHistoryEntry> AddResidenceAsync(int applicationId, string userId, ResidenceInput input);
    Task UpdateResidenceAsync(int applicationId, string userId, int entryId, ResidenceInput input);
    Task RemoveResidenceAsync(int applicationId, string userId, int entryId);

    Task SubmitAsync(int applicationId, string userId);
    Task WithdrawAsync(int applicationId, string userId);

    Task ReviewAsync(int applicationId, string managerId, ReviewOutcome outcome, string? comment, DateOnly? leaseStartDate);

    Task<PagedResult<RentalApplication>> GetListAsync(ApplicationListQuery query);

    /// <summary>Resolves user ids to display names, for showing "who" in the status history table.</summary>
    Task<Dictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds);
}
