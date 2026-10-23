using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Api.Services.Blogs;
using PhotoWorld.Api.Services.Challenges;
using PhotoWorld.Api.Services.Identity;
using PhotoWorld.Api.Services.Moderation;
using PhotoWorld.Api.Services.Notifications;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Api.Services.Push;
using PhotoWorld.Shared.Auth;
using PhotoWorld.Shared.Logging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
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
builder.Services.AddSingleton<AppleSigningKeys>();
builder.Services.AddTransient<AppleIdentityVerifier>();
builder.Services.AddTransient<IExternalIdentityVerifier>(sp => sp.GetRequiredService<AppleIdentityVerifier>());
builder.Services.AddHttpClient<AppleTokenRevoker>();

builder.Services.Configure<SupabaseStorageOptions>(builder.Configuration.GetSection(SupabaseStorageOptions.Section));
builder.Services.AddHttpClient<SupabaseStorage>();
builder.Services.AddSingleton<ImageProcessor>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<BlogHtml>();
builder.Services.AddScoped<ChallengeStandings>();
builder.Services.AddScoped<AdminAccess>();
builder.Services.AddScoped<ActivityFeed>();
builder.Services.AddHostedService<ScheduledActivitySender>();

// Push notifications to phones: off until APNs / FCM keys are set (README → Push notifications).
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.Section));
builder.Services.AddHttpClient<ApnsChannel>().ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { EnableMultipleHttp2Connections = true });
builder.Services.AddHttpClient<FcmChannel>();
builder.Services.AddSingleton<IPushChannel>(sp => sp.GetRequiredService<ApnsChannel>());
builder.Services.AddSingleton<IPushChannel>(sp => sp.GetRequiredService<FcmChannel>());
builder.Services.AddHostedService<PushDispatcher>();
builder.Services.AddMemoryCache();

// Email notifications about new blog posts. Without an SMTP host (development) emails are written to .eml files.
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
var email = builder.Configuration.GetSection(EmailOptions.Section).Get<EmailOptions>() ?? new EmailOptions();
if (email.UsesSmtp) builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else builder.Services.AddSingleton<IEmailSender, PickupDirectoryEmailSender>();
builder.Services.AddSingleton(sp => new NotificationEmails(sp.GetRequiredService<IOptions<EmailOptions>>().Value.WebBaseUrl));
builder.Services.AddHostedService<BlogNotificationSender>();
builder.Services.AddHostedService<WeeklySummarySender>();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Set it with `dotnet user-secrets set Jwt:SigningKey <value>`.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.GetKey(),
        };
        // A token outlives a suspension or a deleted account: check the member is still active (cached briefly),
        // so a suspended member's website and app sessions end within a minute.
        o.Events = new JwtBearerEvents { OnTokenValidated = SessionCheck.ValidateAsync };
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
if (string.IsNullOrWhiteSpace(email.WebBaseUrl))
    app.Logger.LogWarning("Email:WebBaseUrl is not set, so links in notification emails won't work. See README → Email notifications.");
if (!email.UsesSmtp && !app.Environment.IsDevelopment())
    app.Logger.LogWarning("Email:Smtp:Host is not set, so notification emails are only written to {Directory}.", email.PickupDirectory);
if (string.IsNullOrWhiteSpace(app.Configuration[$"{SupabaseStorageOptions.Section}:SecretKey"]))
    app.Logger.LogWarning("Supabase:SecretKey is not set, so photo uploads will fail. See README → Photo storage.");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Interactive API reference at /scalar, reading the document from /openapi/v1.json.
    app.MapScalarApiReference(o => o.AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme));
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
