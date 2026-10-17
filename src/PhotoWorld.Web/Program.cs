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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// CSS, JS and images are endpoints too, so they'd fall under the members-only policy without this.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
