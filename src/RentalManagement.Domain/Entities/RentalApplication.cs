using RentalManagement.Domain.Enums;

namespace RentalManagement.Domain.Entities;

public class RentalApplication
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public bool IsApplicantInfoComplete { get; set; }
    public bool IsResidenceHistoryComplete { get; set; }

    /// <summary>Manually incremented on each successful Residence History section save; used as a concurrency token since that section is a collection, not a single row with a native rowversion.</summary>
    public int ResidenceHistoryVersion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public ApplicantInfo? ApplicantInfo { get; set; }
    public ICollection<ResidenceHistoryEntry> ResidenceHistory { get; set; } = new List<ResidenceHistoryEntry>();
    public ICollection<ApplicationApplicant> Applicants { get; set; } = new List<ApplicationApplicant>();
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    public Lease? Lease { get; set; }

    public byte[]? RowVersion { get; set; }
}
