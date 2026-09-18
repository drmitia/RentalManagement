using RentalManagement.Domain.Enums;

namespace RentalManagement.Domain.Entities;

public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public ApplicationStatus FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
}
