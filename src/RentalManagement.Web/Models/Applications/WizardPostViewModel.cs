using System.ComponentModel.DataAnnotations;

namespace RentalManagement.Web.Models.Applications;

public class WizardPostViewModel
{
    public int ApplicationId { get; set; }
    public WizardSection Section { get; set; }

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string CurrentAddress { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;

    public int ResidenceHistoryVersion { get; set; }
}
