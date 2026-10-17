using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PhotoWorld.Web.Controllers;

public static class ControllerExtensions
{
    /// <summary>True for requests sent by photos.js (fetch), which want JSON or a partial instead of a redirect.</summary>
    public static bool IsFetchRequest(this HttpRequest request) =>
        request.Headers["X-Requested-With"] == "fetch";

    /// <summary>Copies API validation errors onto the form, renaming API field names to view-model names.</summary>
    public static void AddApiErrors(this ModelStateDictionary modelState, IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string> fieldMap)
    {
        foreach (var (key, messages) in errors)
        {
            var field = fieldMap.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? "";
            foreach (var message in messages) modelState.AddModelError(field, message);
        }
    }
}
