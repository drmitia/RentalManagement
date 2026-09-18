using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RentalManagement.Web.Models.Properties;

public class UnitFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Unit number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Range(0, 10)]
    public int Bedrooms { get; set; }

    [Range(0, 100000)]
    [Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Required]
    [Display(Name = "Unit type")]
    public int UnitTypeId { get; set; }

    public List<SelectListItem> UnitTypeOptions { get; set; } = [];
}
