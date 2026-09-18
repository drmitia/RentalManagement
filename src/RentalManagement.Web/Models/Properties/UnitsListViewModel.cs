using RentalManagement.Domain.Entities;

namespace RentalManagement.Web.Models.Properties;

public class UnitsListViewModel
{
    public int PropertyId { get; set; }
    public List<UnitListItemViewModel> Units { get; set; } = [];
}

public class UnitListItemViewModel
{
    public Unit Unit { get; set; } = null!;
    public bool IsAvailable { get; set; }
}
