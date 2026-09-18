using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentalManagement.Domain;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services;
using RentalManagement.Web.Models.Properties;

namespace RentalManagement.Web.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class PropertiesController(IPropertyService propertyService, IUnitService unitService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var properties = await propertyService.GetAllWithUnitsAsync();
        return View(properties);
    }

    public async Task<IActionResult> PropertiesList()
    {
        var properties = await propertyService.GetAllWithUnitsAsync();
        return PartialView("_PropertiesList", properties);
    }

    [HttpGet]
    public async Task<IActionResult> PropertyForm(int? id)
    {
        if (id is null)
        {
            return PartialView("_PropertyForm", new PropertyFormViewModel());
        }

        var property = await propertyService.GetByIdAsync(id.Value);
        if (property is null)
        {
            return NotFound();
        }

        return PartialView("_PropertyForm", new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            Address = property.Address
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PropertyForm(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(model, "_PropertyForm");
        }

        try
        {
            if (model.Id is null)
            {
                await propertyService.CreateAsync(model.Name, model.Address);
            }
            else
            {
                await propertyService.UpdateAsync(model.Id.Value, model.Name, model.Address);
            }
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return UnprocessableEntity(model, "_PropertyForm");
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteProperty(int id)
    {
        var property = await propertyService.GetByIdAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
        {
            Id = id,
            Message = $"Remove property \"{property.Name}\" and all of its units?",
            ActionName = "DeleteProperty"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProperty(int id, bool confirm)
    {
        try
        {
            await propertyService.DeleteAsync(id);
        }
        catch (DomainValidationException ex)
        {
            return UnprocessableEntity(new ConfirmDeleteViewModel
            {
                Id = id,
                Message = ex.Message,
                ActionName = "DeleteProperty"
            }, "_ConfirmDelete");
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> UnitForm(int propertyId, int? id)
    {
        var model = new UnitFormViewModel { PropertyId = propertyId };

        if (id is not null)
        {
            var unit = await unitService.GetByIdAsync(id.Value);
            if (unit is null)
            {
                return NotFound();
            }

            model.Id = unit.Id;
            model.UnitNumber = unit.UnitNumber;
            model.Bedrooms = unit.Bedrooms;
            model.MonthlyRent = unit.MonthlyRent;
            model.UnitTypeId = unit.UnitTypeId;
        }

        model.UnitTypeOptions = await BuildUnitTypeOptionsAsync(model.UnitTypeId);
        return PartialView("_UnitForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnitForm(UnitFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.UnitTypeOptions = await BuildUnitTypeOptionsAsync(model.UnitTypeId);
            return UnprocessableEntity(model, "_UnitForm");
        }

        try
        {
            if (model.Id is null)
            {
                await unitService.CreateAsync(model.PropertyId, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
            }
            else
            {
                await unitService.UpdateAsync(model.Id.Value, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
            }
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            model.UnitTypeOptions = await BuildUnitTypeOptionsAsync(model.UnitTypeId);
            return UnprocessableEntity(model, "_UnitForm");
        }

        return Json(new { success = true, propertyId = model.PropertyId });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteUnit(int id)
    {
        var unit = await unitService.GetByIdAsync(id);
        if (unit is null)
        {
            return NotFound();
        }

        return PartialView("_ConfirmDelete", new ConfirmDeleteViewModel
        {
            Id = id,
            Message = $"Remove unit \"{unit.UnitNumber}\"?",
            ActionName = "DeleteUnit"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnit(int id, bool confirm)
    {
        try
        {
            await unitService.DeleteAsync(id);
        }
        catch (DomainValidationException ex)
        {
            return UnprocessableEntity(new ConfirmDeleteViewModel
            {
                Id = id,
                Message = ex.Message,
                ActionName = "DeleteUnit"
            }, "_ConfirmDelete");
        }

        return Json(new { success = true });
    }

    private async Task<List<SelectListItem>> BuildUnitTypeOptionsAsync(int selectedId)
    {
        var types = await unitService.GetUnitTypesAsync();
        return types
            .Where(t => t.IsActive || t.Id == selectedId)
            .Select(t => new SelectListItem(t.IsActive ? t.Name : $"{t.Name} (inactive)", t.Id.ToString(), t.Id == selectedId))
            .ToList();
    }

    private IActionResult UnprocessableEntity(object model, string viewName)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView(viewName, model);
    }
}
