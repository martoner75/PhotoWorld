using PhotoWorld.Web.Authentication;
using PhotoWorld.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
