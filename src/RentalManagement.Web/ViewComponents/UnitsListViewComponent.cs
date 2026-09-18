using Microsoft.AspNetCore.Mvc;
using RentalManagement.Domain.Services;
using RentalManagement.Web.Models.Properties;

namespace RentalManagement.Web.ViewComponents;

public class UnitsListViewComponent(IUnitService unitService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int propertyId)
    {
        var units = await unitService.GetByPropertyIdAsync(propertyId);

        var items = new List<UnitListItemViewModel>();
        foreach (var unit in units)
        {
            items.Add(new UnitListItemViewModel
            {
                Unit = unit,
                IsAvailable = await unitService.IsAvailableAsync(unit.Id, DateOnly.FromDateTime(DateTime.UtcNow))
            });
        }

        return View(new UnitsListViewModel { PropertyId = propertyId, Units = items });
    }
}
