using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;

namespace RentalManagement.Web.Models.Applications;

public enum WizardSection
{
    ApplicantInfo,
    ResidenceHistory,
    Summary
}

public class WizardViewModel
{
    public int ApplicationId { get; set; }
    public WizardSection Section { get; set; }
    public bool IsEditable { get; set; }
    public ApplicationStatus Status { get; set; }

    public Unit Unit { get; set; } = null!;

    public ApplicantInfoSectionViewModel ApplicantInfo { get; set; } = new();
    public int ResidenceHistoryVersion { get; set; }

    public bool IsApplicantInfoComplete { get; set; }
    public bool IsResidenceHistoryComplete { get; set; }

    public string? ErrorMessage { get; set; }
}
