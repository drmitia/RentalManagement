using System.ComponentModel.DataAnnotations;
using RentalManagement.Domain.Enums;

namespace RentalManagement.Web.Models.Applications;

public class ReviewFormViewModel
{
    public int ApplicationId { get; set; }
    public string UnitLabel { get; set; } = string.Empty;

    [Required]
    public ReviewOutcome Outcome { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Lease start date")]
    public DateOnly? LeaseStartDate { get; set; }
}
