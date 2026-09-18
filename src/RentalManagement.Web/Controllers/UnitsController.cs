using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Domain;
using RentalManagement.Domain.Services;

namespace RentalManagement.Web.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class UnitsController(IUnitService unitService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var units = await unitService.GetAvailableAsync();
        return View(units);
    }
}
