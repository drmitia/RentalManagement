using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Services;
using RentalManagement.Infrastructure.Identity;
using RentalManagement.Web.Models.Applications;

namespace RentalManagement.Web.ViewComponents;

public class ResidenceHistoryListViewComponent(
    IApplicationService applicationService,
    UserManager<ApplicationUser> userManager) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int applicationId, bool isManagerView, bool forceReadOnly = false)
    {
        var application = isManagerView
            ? await applicationService.GetForManagerAsync(applicationId)
            : await applicationService.GetForApplicantAsync(applicationId, userManager.GetUserId(UserClaimsPrincipal)!);

        if (application is null)
        {
            return View(new ResidenceHistoryListViewModel());
        }

        var isEditable = !forceReadOnly && !isManagerView && application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;

        return View(new ResidenceHistoryListViewModel
        {
            ApplicationId = applicationId,
            IsEditable = isEditable,
            ResidenceHistoryVersion = application.ResidenceHistoryVersion,
            Entries = application.ResidenceHistory.OrderBy(r => r.MoveInDate).ToList()
        });
    }
}
