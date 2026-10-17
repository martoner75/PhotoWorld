using Microsoft.AspNetCore.Mvc;
using PhotoWorld.Api.Services.Moderation;

namespace PhotoWorld.Api.Controllers;

internal static class ReportResponses
{
    /// <summary>204 when saved, 400 for an unknown reason, 409 when this member already reported the item.</summary>
    public static IActionResult ToResponse(this ControllerBase controller, ReportResult result)
    {
        switch (result)
        {
            case ReportResult.InvalidReason:
                controller.ModelState.AddModelError("Reason", "Choose a reason.");
                return controller.ValidationProblem(controller.ModelState);
            case ReportResult.AlreadyReported:
                return controller.Problem("You've already reported this.", statusCode: StatusCodes.Status409Conflict);
            default:
                return controller.NoContent();
        }
    }
}
