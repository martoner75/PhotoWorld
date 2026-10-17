# PhotoWorld

A portal for sharing a passion for photography with people around the world.
ASP.NET Core MVC on **.NET 10**, split into an API, a UI and a shared library.

```
PhotoWorld.slnx
├── src/PhotoWorld.Shared   Class library: DTOs, API routes, provider names, claim types, Supabase logger (used by both)
├── src/PhotoWorld.Api      Web API: users (Supabase Postgres + EF Core), provider token verification, JWT issuing
└── src/PhotoWorld.Web      MVC UI: pages, cookie auth, Google / Facebook / Instagram sign-in
```

## How sign-in works

1. The user clicks **Continue with Google / Facebook / Instagram** on `/Account/Login` (or `/Account/Register`).
2. **Web** runs the OAuth flow with the provider and receives the provider's access token.
3. **Web** sends it to **API** `POST /api/auth/external`. The API checks the token **directly with the provider**
   (Google `tokeninfo`, Facebook `debug_token`, Instagram `/me`), confirming it was issued to PhotoWorld.
4. The API finds or **registers** the user (first sign-in = registration). An account with the same verified
   email from another provider is linked, so it doesn't create a duplicate. It then returns a PhotoWorld JWT.
5. **Web** signs the user in with a cookie that carries the JWT and attaches it to later API calls (`GET /api/users/me`).

## Supabase (database + logs)

All data lives in a Supabase Postgres database, in a dedicated **`photoworld`** schema. It's kept out of
`public` on purpose: Supabase's auto-generated Data API exposes `public` tables to anyone holding the anon key.

| Table | Written by | Contents |
|---|---|---|
| `photoworld.users`, `photoworld.external_logins` | API (EF Core) | Members and their linked Google/Facebook/Instagram accounts |
| `photoworld.app_logs` | API **and** Web (`SupabaseLoggerProvider`) | Application logs, tagged with `application` = `PhotoWorld.Api` / `PhotoWorld.Web` |
| `photoworld.__ef_migrations_history` | API | Applied EF migrations |

**1. Connect the API.** In the Supabase dashboard, click **Connect** and copy the **Session pooler**
connection string in .NET format. (The direct connection is IPv6-only on the free plan; the transaction
pooler doesn't support the bulk COPY the logger uses.)

```bash
dotnet user-secrets set "ConnectionStrings:PhotoWorld" "User Id=postgres.<project-ref>;Password=<db-password>;Server=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;SSL Mode=Require" --project src/PhotoWorld.Api
```

Start the API once. It applies the EF migrations, which create the schema and all tables.
To create them by hand instead, run [`docs/supabase-schema.sql`](docs/supabase-schema.sql) in the SQL editor.

**2. Logging connection.** The UI must not hold the main database password, so it logs through an
insert-only role. Run [`docs/supabase-logging-role.sql`](docs/supabase-logging-role.sql) in the SQL editor
(change the password first), then:

```bash
dotnet user-secrets set "ConnectionStrings:Logging" "User Id=photoworld_logger.<project-ref>;Password=<logger-password>;Server=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;SSL Mode=Require" --project src/PhotoWorld.Web
```

The API logs with `ConnectionStrings:Logging` if set, otherwise its main connection. Without any logging
connection string, Web still runs and logs to the console only.

Logs are batched in memory and bulk-inserted every ~2 seconds, so logging never slows down a request.
If Supabase is unreachable, that batch is dropped and a `[SupabaseLogger]` line goes to stderr.
What gets stored is controlled by `Logging:Supabase:LogLevel` in each `appsettings.json` (default: Information,
framework noise at Warning).

```sql
-- Latest errors from either app
select timestamp, application, category, message, exception
from photoworld.app_logs where level in ('Error', 'Critical') order by timestamp desc limit 50;

-- Follow one request across Web and API
select timestamp, application, message, properties from photoworld.app_logs
where trace_id = '<trace id>' order by timestamp;
```

New EF migrations: `dotnet ef migrations add <Name> --project src/PhotoWorld.Api --output-dir Data/Migrations`
(`dotnet tool restore` first installs the repo's `dotnet-ef`).

## Run locally

```bash
dotnet dev-certs https --trust
dotnet run --project src/PhotoWorld.Api --launch-profile https   # https://localhost:7041
dotnet run --project src/PhotoWorld.Web --launch-profile https   # https://localhost:7211
```

In Rider / Visual Studio, use the **Web + API** launch profile (`PhotoWorld.slnLaunch`).

The API's JWT signing key is already in user-secrets on this machine. On a new machine:

```bash
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/PhotoWorld.Api
```

The login page works without provider credentials, but the buttons stay disabled until you configure them.

## Configure providers

Credentials go in **user-secrets** (never in `appsettings.json`). Web needs ClientId + ClientSecret.
The API needs the same IDs to check that tokens were issued to PhotoWorld.

### Google
1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → Create OAuth client ID → *Web application*.
2. Authorized redirect URI: `https://localhost:7211/signin-google`
```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/PhotoWorld.Api
```

### Facebook
1. [Meta for Developers](https://developers.facebook.com/apps) → Create app → add **Facebook Login**.
2. Valid OAuth redirect URI: `https://localhost:7211/signin-facebook`
```bash
dotnet user-secrets set "Authentication:Facebook:ClientId" "<app id>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Facebook:ClientSecret" "<app secret>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Facebook:ClientId" "<app id>" --project src/PhotoWorld.Api
dotnet user-secrets set "Authentication:Facebook:ClientSecret" "<app secret>" --project src/PhotoWorld.Api
```

### Instagram
> **Limitation:** Meta retired the Instagram Basic Display API in December 2024. Sign-in now uses
> *Instagram API with Instagram Login*, which only works for **Business and Creator** accounts, and
> Instagram does not share email addresses. Personal Instagram users can sign in with Facebook instead.

1. In your Meta app, add the **Instagram** product → *API setup with Instagram login*.
2. OAuth redirect URI: `https://localhost:7211/signin-instagram`
```bash
dotnet user-secrets set "Authentication:Instagram:ClientId" "<instagram app id>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Instagram:ClientSecret" "<instagram app secret>" --project src/PhotoWorld.Web
```

## Notes / next steps
- The API applies migrations on startup. Once it runs on several instances, apply them from CI instead.
- `app_logs` grows forever. Add a retention job (e.g. `pg_cron` deleting rows older than 30 days).
- Facebook and Instagram require a public privacy policy URL. Fill in `Views/Home/Privacy.cshtml`.
- Photos (upload, gallery, profiles) are the next feature to build on top of this skeleton.
