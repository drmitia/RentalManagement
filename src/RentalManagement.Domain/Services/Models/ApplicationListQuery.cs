using RentalManagement.Domain.Enums;

namespace RentalManagement.Domain.Services.Models;

public record ApplicationListQuery(
    ApplicationStatus? Status,
    int? PropertyId,
    string? ApplicantUserId,
    int Page = 1,
    int PageSize = 20);

public record PagedResult<T>(List<T> Items, int TotalCount);
