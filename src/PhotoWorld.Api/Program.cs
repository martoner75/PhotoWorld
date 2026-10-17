using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Supabase Postgres. Use the "Session pooler" connection string from the Supabase dashboard (Connect button).
var connectionString = builder.Configuration.GetConnectionString("PhotoWorld");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:PhotoWorld is not set. See README → Supabase.");
// Logs go to photoworld.app_logs. A dedicated insert-only role (ConnectionStrings:Logging) is preferred;
// otherwise the main connection is used.
builder.Logging.AddSupabase(builder.Configuration.GetConnectionString("Logging") ?? connectionString, "PhotoWorld.Api");

builder.Services.AddDbContext<PhotoWorldDbContext>(o => o
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PhotoWorldDbContext.Schema))
    .UseSnakeCaseNamingConvention());

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

// Apply pending EF migrations on startup. Fine for a single instance; run `dotnet ef database update`
// (or a migration bundle) from CI instead once the API scales out.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>().Database.Migrate();

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
