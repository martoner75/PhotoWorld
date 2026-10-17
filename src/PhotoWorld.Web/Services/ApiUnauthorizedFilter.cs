using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PhotoWorld.Web.Services;

/// <summary>
/// When the API says the user's token is no longer valid, end the cookie session too and send them
/// back to sign in, returning to the page they were on.
/// </summary>
public sealed class ApiUnauthorizedFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiUnauthorizedException) return;

        var http = context.HttpContext;
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var returnUrl = HttpMethods.IsGet(http.Request.Method) ? http.Request.Path + http.Request.QueryString : "/";
        context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
        context.ExceptionHandled = true;
    }
}
