using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Photos;
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
var loggingConnectionString = builder.Configuration.GetConnectionString("Logging");
builder.Logging.AddSupabase(
    string.IsNullOrWhiteSpace(loggingConnectionString) ? connectionString : loggingConnectionString, "PhotoWorld.Api");

builder.Services.AddDbContext<PhotoWorldDbContext>(o => o
    .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PhotoWorldDbContext.Schema))
    .UseSnakeCaseNamingConvention());

// Provider credentials live under Authentication:{Provider}, the same shape the Web project uses.
foreach (var provider in AuthProviders.All)
    builder.Services.Configure<ExternalProviderOptions>(provider, builder.Configuration.GetSection($"Authentication:{provider}"));

builder.Services.AddHttpClient<GoogleIdentityVerifier>();
builder.Services.AddTransient<IExternalIdentityVerifier>(sp => sp.GetRequiredService<GoogleIdentityVerifier>());

builder.Services.Configure<SupabaseStorageOptions>(builder.Configuration.GetSection(SupabaseStorageOptions.Section));
builder.Services.AddHttpClient<SupabaseStorage>();
builder.Services.AddSingleton<ImageProcessor>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<BlogHtml>();

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

// Migrations are applied by CI (.github/workflows/ci.yml) or `dotnet ef database update`, never here,
// so several API instances can't race each other. Just warn if the database is behind the code.
using (var scope = app.Services.CreateScope())
{
    var pending = (await scope.ServiceProvider.GetRequiredService<PhotoWorldDbContext>().Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
        app.Logger.LogWarning("Database is missing {Count} migration(s): {Migrations}. Run `dotnet ef database update`.",
            pending.Count, string.Join(", ", pending));
}
if (string.IsNullOrWhiteSpace(app.Configuration[$"{SupabaseStorageOptions.Section}:SecretKey"]))
    app.Logger.LogWarning("Supabase:SecretKey is not set, so photo uploads will fail. See README → Photo storage.");

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
