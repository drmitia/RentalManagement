using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RentalManagement.Domain;
using RentalManagement.Domain.Entities;
using RentalManagement.Domain.Enums;
using RentalManagement.Domain.Exceptions;
using RentalManagement.Domain.Services;
using RentalManagement.Domain.Services.Models;
using RentalManagement.Infrastructure.Identity;
using RentalManagement.Web.Models.Applications;

namespace RentalManagement.Web.Controllers;

[Authorize]
public class ApplicationsController(
    IApplicationService applicationService,
    IPropertyService propertyService,
    UserManager<ApplicationUser> userManager) : Controller
{
    private string CurrentUserId => userManager.GetUserId(User)!;
    private bool IsManager => User.IsInRole(Roles.PropertyManager);

    public async Task<IActionResult> Index(ApplicationStatus? status, int? propertyId, int page = 1)
    {
        var query = new ApplicationListQuery(
            status,
            propertyId,
            IsManager ? null : CurrentUserId,
            page);

        var result = await applicationService.GetListAsync(query);

        ViewBag.Properties = new SelectList(await propertyService.GetLookupListAsync(), "Id", "Name", propertyId);
        ViewBag.Status = status;
        ViewBag.PropertyId = propertyId;
        ViewBag.Page = page;
        ViewBag.TotalCount = result.TotalCount;
        ViewBag.PageSize = query.PageSize;

        return View(result.Items);
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int unitId)
    {
        var application = await applicationService.StartOrResumeDraftAsync(unitId, CurrentUserId);
        return RedirectToAction(nameof(Wizard), new { id = application.Id });
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpGet]
    public async Task<IActionResult> Wizard(int id, WizardSection? section)
    {
        var model = await BuildWizardViewModelAsync(id, section);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [Authorize(Roles = Roles.Applicant)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(WizardPostViewModel post, string button)
    {
        if (post.Section != WizardSection.ApplicantInfo)
        {
            string[] applicantInfoKeys = [nameof(post.FullName), nameof(post.Phone), nameof(post.Email), nameof(post.CurrentAddress)];
            foreach (var key in applicantInfoKeys)
            {
                ModelState.Remove(key);
            }
        }

        if (button == "withdraw")
        {
            return await TryOrReload(post.ApplicationId, post.Section,
                () => applicationService.WithdrawAsync(post.ApplicationId, CurrentUserId),
                () => RedirectToAction(nameof(Index)));
        }

        if (button == "submit" && post.Section == WizardSection.Summary)
        {
            return await TryOrReload(post.ApplicationId, post.Section,
                () => applicationService.SubmitAsync(post.ApplicationId, CurrentUserId),
                () => RedirectToAction(nameof(Index)));
        }

        if (button == "continue")
        {
            if (post.Section == WizardSection.ApplicantInfo)
            {
                if (!ModelState.IsValid)
                {
                    var reload = await BuildWizardViewModelAsync(post.ApplicationId, post.Section);
                    if (reload is null) return NotFound();
                    reload.ApplicantInfo.FullName = post.FullName;
                    reload.ApplicantInfo.Phone = post.Phone;
                    reload.ApplicantInfo.Email = post.Email;
                    reload.ApplicantInfo.CurrentAddress = post.CurrentAddress;
                    reload.ApplicantInfo.IsEditable = true;
                    return View(reload);
                }

                return await TryOrReload(post.ApplicationId, post.Section,
                    () => applicationService.SaveApplicantInfoAsync(post.ApplicationId, CurrentUserId,
                        new ApplicantInfoInput(
                            post.FullName,
                            post.Phone,
                            post.Email,
                            post.CurrentAddress,
                            string.IsNullOrEmpty(post.RowVersion) ? null : Convert.FromBase64String(post.RowVersion))),
                    () => RedirectToAction(nameof(Wizard), new { id = post.ApplicationId, section = WizardSection.ResidenceHistory }));
            }

            if (post.Section == WizardSection.ResidenceHistory)
            {
                return await TryOrReload(post.ApplicationId, post.Section,
                    () => applicationService.MarkResidenceHistoryReviewedAsync(post.ApplicationId, CurrentUserId, post.ResidenceHistoryVersion),
                    () => RedirectToAction(nameof(Wizard), new { id = post.ApplicationId, section = WizardSection.Summary }));
            }
        }

        return RedirectToAction(nameof(Wizard), new { id = post.ApplicationId, section = post.Section });
    }

    public async Task<IActionResult> Details(int id)
    {
        var application = await LoadDetailsAsync(id);
        return application is null ? NotFound() : View(application);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsPartial(int id)
    {
        var application = await LoadDetailsAsync(id);
        return application is null ? NotFound() : PartialView("_ApplicationDetailsContent", application);
    }

    private async Task<RentalApplication?> LoadDetailsAsync(int id)
    {
        var application = IsManager
            ? await applicationService.GetForManagerAsync(id)
            : await applicationService.GetForApplicantAsync(id, CurrentUserId);

        if (application is null)
        {
            return null;
        }

        var userIds = application.StatusHistory.Select(h => h.ChangedByUserId)
            .Concat(application.Applicants.Select(a => a.UserId))
            .Distinct();
        var userNames = await applicationService.GetUserNamesAsync(userIds);

        ViewBag.IsManager = IsManager;
        ViewBag.UserNames = userNames;
        return application;
    }

    [HttpGet]
    public IActionResult ResidenceHistoryPartial(int applicationId) =>
        ViewComponent("ResidenceHistoryList", new { applicationId, isManagerView = false });

    [HttpGet]
    public async Task<IActionResult> ResidenceForm(int applicationId, int? id)
    {
        var application = await applicationService.GetForApplicantAsync(applicationId, CurrentUserId);
        if (application is null)
        {
            return NotFound();
        }

        var model = new ResidenceFormViewModel { ApplicationId = applicationId };
        if (id is not null)
        {
            var entry = application.ResidenceHistory.FirstOrDefault(r => r.Id == id);
            if (entry is null) return NotFound();

            model.Id = entry.Id;
            model.Address = entry.Address;
            model.LandlordName = entry.LandlordName;
            model.LandlordPhone = entry.LandlordPhone;
            model.MoveInDate = entry.MoveInDate;
            model.MoveOutDate = entry.MoveOutDate;
        }

        return PartialView("_ResidenceForm", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResidenceForm(ResidenceFormViewModel model)
    {
        if (model.MoveOutDate is not null && model.MoveInDate is not null && model.MoveOutDate < model.MoveInDate)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate), "Move-out date cannot be before the move-in date.");
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(model, "_ResidenceForm");
        }

        try
        {
            var input = new ResidenceInput(model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate!.Value, model.MoveOutDate);
            if (model.Id is null)
            {
                await applicationService.AddResidenceAsync(model.ApplicationId, CurrentUserId, input);
            }
            else
            {
                await applicationService.UpdateResidenceAsync(model.ApplicationId, CurrentUserId, model.Id.Value, input);
            }
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return UnprocessableEntity(model, "_ResidenceForm");
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteResidence(int applicationId, int id)
    {
        var application = await applicationService.GetForApplicantAsync(applicationId, CurrentUserId);
        var entry = application?.ResidenceHistory.FirstOrDefault(r => r.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        return PartialView("_ConfirmDeleteResidence", new ConfirmDeleteResidenceViewModel
        {
            ApplicationId = applicationId,
            Id = id,
            Message = $"Remove residence at \"{entry.Address}\"?"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResidence(int applicationId, int id, bool confirm)
    {
        try
        {
            await applicationService.RemoveResidenceAsync(applicationId, CurrentUserId, id);
        }
        catch (DomainValidationException ex)
        {
            return UnprocessableEntity(new ConfirmDeleteResidenceViewModel
            {
                ApplicationId = applicationId,
                Id = id,
                Message = ex.Message
            }, "_ConfirmDeleteResidence");
        }

        return Json(new { success = true });
    }

    [Authorize(Roles = Roles.PropertyManager)]
    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var application = await applicationService.GetForManagerAsync(id);
        if (application is null || application.Status != ApplicationStatus.Submitted)
        {
            return NotFound();
        }

        return PartialView("_ReviewForm", new ReviewFormViewModel
        {
            ApplicationId = id,
            UnitLabel = $"{application.Unit.Property.Name} - Unit {application.Unit.UnitNumber}",
            LeaseStartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
    }

    [Authorize(Roles = Roles.PropertyManager)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewFormViewModel model)
    {
        if (model.Outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(model.Comment))
        {
            ModelState.AddModelError(nameof(model.Comment), "A comment is required to return or deny an application.");
        }
        if (model.Outcome == ReviewOutcome.Approve && model.LeaseStartDate is null)
        {
            ModelState.AddModelError(nameof(model.LeaseStartDate), "A lease start date is required.");
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(model, "_ReviewForm");
        }

        try
        {
            await applicationService.ReviewAsync(model.ApplicationId, CurrentUserId, model.Outcome, model.Comment, model.LeaseStartDate);
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return UnprocessableEntity(model, "_ReviewForm");
        }

        return Json(new { success = true });
    }

    private async Task<WizardViewModel?> BuildWizardViewModelAsync(int applicationId, WizardSection? requestedSection)
    {
        var application = await applicationService.GetForApplicantAsync(applicationId, CurrentUserId);
        if (application is null)
        {
            return null;
        }

        var isEditable = application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;
        var section = isEditable
            ? requestedSection ?? DefaultSectionFor(application)
            : WizardSection.Summary;

        return new WizardViewModel
        {
            ApplicationId = application.Id,
            Section = section,
            IsEditable = isEditable,
            Status = application.Status,
            Unit = application.Unit,
            IsApplicantInfoComplete = application.IsApplicantInfoComplete,
            IsResidenceHistoryComplete = application.IsResidenceHistoryComplete,
            ResidenceHistoryVersion = application.ResidenceHistoryVersion,
            ApplicantInfo = new ApplicantInfoSectionViewModel
            {
                ApplicationId = application.Id,
                FullName = application.ApplicantInfo?.FullName ?? string.Empty,
                Phone = application.ApplicantInfo?.Phone ?? string.Empty,
                Email = application.ApplicantInfo?.Email ?? string.Empty,
                CurrentAddress = application.ApplicantInfo?.CurrentAddress ?? string.Empty,
                RowVersion = Convert.ToBase64String(application.ApplicantInfo?.RowVersion ?? []),
                IsEditable = isEditable
            }
        };
    }

    private static WizardSection DefaultSectionFor(RentalApplication application)
    {
        if (!application.IsApplicantInfoComplete) return WizardSection.ApplicantInfo;
        if (!application.IsResidenceHistoryComplete) return WizardSection.ResidenceHistory;
        return WizardSection.Summary;
    }

    private async Task<IActionResult> TryOrReload(int applicationId, WizardSection section, Func<Task> action, Func<IActionResult> onSuccess)
    {
        try
        {
            await action();
        }
        catch (DomainValidationException ex)
        {
            var reload = await BuildWizardViewModelAsync(applicationId, section);
            if (reload is null) return NotFound();
            reload.ErrorMessage = ex.Message;
            return View("Wizard", reload);
        }

        return onSuccess();
    }

    private IActionResult UnprocessableEntity(object model, string viewName)
    {
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        return PartialView(viewName, model);
    }
}
