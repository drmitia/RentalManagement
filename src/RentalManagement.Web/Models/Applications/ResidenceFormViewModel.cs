using System.ComponentModel.DataAnnotations;

namespace RentalManagement.Web.Models.Applications;

public class ResidenceFormViewModel
{
    public int? Id { get; set; }
    public int ApplicationId { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Landlord name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    [Display(Name = "Landlord phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveInDate { get; set; }

    [Display(Name = "Move-out date")]
    [DataType(DataType.Date)]
    public DateOnly? MoveOutDate { get; set; }
}
