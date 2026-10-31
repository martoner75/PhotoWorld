using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using PhotoWorld.Shared.Logging;
using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// The UI only gets the insert-only logging connection, never the main database credentials.
builder.Logging.AddSupabase(builder.Configuration.GetConnectionString("Logging"), "PhotoWorld.Web");

// Members-only: every page needs a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddControllersWithViews(o => o.Filters.Add<ApiUnauthorizedFilter>());
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddPhotoWorldAuthentication(builder.Configuration);

builder.Services.AddTransient<ApiAccessTokenHandler>();
builder.Services.AddHttpClient<IPhotoWorldApiClient, PhotoWorldApiClient>(c =>
        c.BaseAddress = new Uri(builder.Configuration["PhotoWorldApi:BaseUrl"]
            ?? throw new InvalidOperationException("PhotoWorldApi:BaseUrl is not configured.")))
    .AddHttpMessageHandler<ApiAccessTokenHandler>();

// Rate limiting, per visitor address. Static files (anything with a file extension) are exempt so a page load can't use
// up the allowance. Reads and form posts have separate budgets per minute; 0 under RateLimiting switches one off.
// Behind a reverse proxy every visitor shares the proxy's address unless forwarded headers are configured.
var readsPerMinute = builder.Configuration.GetValue("RateLimiting:ReadsPerMinute", 600);
var postsPerMinute = builder.Configuration.GetValue("RateLimiting:PostsPerMinute", 60);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var post = !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method)
                   && !HttpMethods.IsOptions(ctx.Request.Method);
        var limit = post ? postsPerMinute : readsPerMinute;
        var address = ctx.Connection.RemoteIpAddress?.ToString();
        if (address is null || limit <= 0 || Path.HasExtension(ctx.Request.Path.Value)) return RateLimitPartition.GetNoLimiter("unlimited");
        return RateLimitPartition.GetFixedWindowLimiter($"{(post ? "p" : "r")}:{address}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        });
    });
    o.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// CSS, JS and images are endpoints too, so they'd fall under the members-only policy without this.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
