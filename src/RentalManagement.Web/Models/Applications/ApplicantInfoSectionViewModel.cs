using System.ComponentModel.DataAnnotations;

namespace RentalManagement.Web.Models.Applications;

public class ApplicantInfoSectionViewModel
{
    public int ApplicationId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(300)]
    [Display(Name = "Current address")]
    public string CurrentAddress { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public bool IsEditable { get; set; }
}
