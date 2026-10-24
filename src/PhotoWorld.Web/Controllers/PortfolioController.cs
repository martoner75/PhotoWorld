using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Portfolios;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

/// <summary>Public portfolios at /p/{slug} (open to everyone) and the owner's settings page.</summary>
public class PortfolioController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> Fields = new()
    {
        ["slug"] = nameof(PortfolioEditViewModel.Slug),
        ["bio"] = nameof(PortfolioEditViewModel.Bio),
        ["website"] = nameof(PortfolioEditViewModel.Website),
        ["instagram"] = nameof(PortfolioEditViewModel.Instagram),
        ["contactEmail"] = nameof(PortfolioEditViewModel.ContactEmail),
        ["photoIds"] = nameof(PortfolioEditViewModel.PhotoIds),
    };

    [AllowAnonymous]
    [HttpGet("/p/{slug}")]
    public async Task<IActionResult> Show(string slug, CancellationToken ct)
    {
        var portfolio = PortfolioRules.IsValidSlug(slug.ToLowerInvariant()) ? await api.GetPortfolioAsync(slug, ct) : null;
        return portfolio is null ? NotFound() : View(portfolio);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var mine = await api.GetMyPortfolioAsync(ct);
        return mine is null ? NotFound() : View(PortfolioEditViewModel.From(mine));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PortfolioEditViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await api.SavePortfolioAsync(new SavePortfolioRequest(model.IsPublic, model.Slug, model.Bio,
                model.Website, model.Instagram, model.ContactEmail, model.OrderedPhotoIds()), ct);
            if (result.Succeeded)
            {
                TempData["Flash"] = model.IsPublic
                    ? "Portfolio saved. It's public: share the link with anyone."
                    : "Portfolio saved. It's switched off, so only you can see these settings.";
                return RedirectToAction(nameof(Edit));
            }
            ModelState.AddApiErrors(result.Errors, Fields);
        }
        model.Candidates = (await api.GetMyPortfolioAsync(ct))?.Candidates ?? [];
        return View(model);
    }
}
