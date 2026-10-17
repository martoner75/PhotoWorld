using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Shared.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<PhotoWorldDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("PhotoWorld")));

// Provider credentials live under Authentication:{Provider}, the same shape the Web project uses.
foreach (var provider in AuthProviders.All)
    builder.Services.Configure<ExternalProviderOptions>(provider, builder.Configuration.GetSection($"Authentication:{provider}"));

builder.Services.AddHttpClient<GoogleIdentityVerifier>();
builder.Services.AddHttpClient<FacebookIdentityVerifier>();
builder.Services.AddHttpClient<InstagramIdentityVerifier>();
builder.Services.AddTransient<IExternalIdentityVerifier>(sp => sp.GetRequiredService<GoogleIdentityVerifier>());
builder.Services.AddTransient<IExternalIdentityVerifier>(sp => sp.GetRequiredService<FacebookIdentityVerifier>());
builder.Services.AddTransient<IExternalIdentityVerifier>(sp => sp.GetRequiredService<InstagramIdentityVerifier>());

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Set it with `dotnet user-secrets set Jwt:SigningKey <value>`.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.GetKey(),
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// POC convenience: create the SQLite schema on first run. Switch to EF migrations before production.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>().Database.EnsureCreated();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
