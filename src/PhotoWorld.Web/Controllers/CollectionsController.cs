using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Web.Models;
using PhotoWorld.Web.Services;

namespace PhotoWorld.Web.Controllers;

public class CollectionsController(IPhotoWorldApiClient api) : Controller
{
    private static readonly Dictionary<string, string> FormFields = new()
    {
        ["title"] = nameof(CollectionFormViewModel.Title),
        ["description"] = nameof(CollectionFormViewModel.Description),
    };

    /// <summary>The signed-in member's collections, private ones included.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new CollectionsPageViewModel(await api.GetCollectionsAsync(ct: ct), null));

    /// <summary>Another member's public collections.</summary>
    [HttpGet]
    public async Task<IActionResult> Member(Guid id, CancellationToken ct)
    {
        var member = await api.GetMemberAsync(id, ct);
        if (member is null) return NotFound();
        if (member.IsMe) return RedirectToAction(nameof(Index));
        return View(nameof(Index), new CollectionsPageViewModel(await api.GetCollectionsAsync(id, ct), member));
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, int page = 1, CancellationToken ct = default)
    {
        var collection = await api.GetCollectionAsync(id, page, ct);
        return collection is null ? NotFound() : View(collection);
    }

    [HttpGet]
    public IActionResult New(Guid? photoId) => View("Form", new CollectionFormViewModel());

    /// <summary>Creates a collection; with <paramref name="photoId"/> (from a photo's Save page) the photo goes straight in.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(CollectionFormViewModel model, Guid? photoId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return photoId is null ? View("Form", model) : BackToSave(photoId.Value, "Give the collection a name.");
        var result = await api.CreateCollectionAsync(model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            if (photoId is { } p) return BackToSave(p, result.Errors.Values.SelectMany(m => m).FirstOrDefault());
            ModelState.AddApiErrors(result.Errors, FormFields);
            return View("Form", model);
        }

        var created = result.Value!;
        if (photoId is { } photo)
        {
            TempData["Flash"] = await api.SetCollectionPhotoAsync(created.Id, photo, true, ct) ?? $"Saved to “{created.Title}”.";
            return RedirectToAction(nameof(Save), new { photoId = photo });
        }
        TempData["Flash"] = $"Collection “{created.Title}” created. Use Save on any photo to add it.";
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var collection = await api.GetCollectionAsync(id, 1, ct);
        if (collection is null || !collection.Collection.IsMine) return NotFound();
        var c = collection.Collection;
        return View("Form", new CollectionFormViewModel { Id = c.Id, Title = c.Title, Description = c.Description, IsPublic = c.IsPublic });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CollectionFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);
        var result = await api.UpdateCollectionAsync(id, model.ToRequest(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result.Errors, FormFields);
            return View("Form", model);
        }
        TempData["Flash"] = "Collection saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        TempData["Flash"] = await api.DeleteCollectionAsync(id, ct)
            ? "Collection deleted. The photos themselves weren't touched."
            : "That collection no longer exists.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Choose which of your collections a photo is in.</summary>
    [HttpGet]
    public async Task<IActionResult> Save(Guid photoId, CancellationToken ct)
    {
        var photo = await api.GetPhotoAsync(photoId, ct);
        if (photo is null) return NotFound();
        return View(new SavePhotoViewModel(photo, await api.GetCollectionChoicesAsync(photoId, ct)));
    }

    /// <summary>Puts a photo in a collection or takes it out (from the Save page, or "Remove" on a collection).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, Guid photoId, bool add, string? returnUrl, CancellationToken ct)
    {
        TempData["Flash"] = await api.SetCollectionPhotoAsync(id, photoId, add, ct) ?? (add ? "Saved." : "Removed from the collection.");
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Save), new { photoId });
    }

    private IActionResult BackToSave(Guid photoId, string? message)
    {
        TempData["Flash"] = message ?? "We couldn't create that collection.";
        return RedirectToAction(nameof(Save), new { photoId });
    }
}
