# PhotoWorld

A portal for sharing a passion for photography with people around the world.
ASP.NET Core MVC on **.NET 10**, split into an API, a UI and a shared library.

```
PhotoWorld.slnx
├── src/PhotoWorld.Shared   Class library: DTOs, API routes, provider names, claim types (used by both)
├── src/PhotoWorld.Api      Web API: users (SQLite + EF Core), provider token verification, JWT issuing
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
- The SQLite DB (`photoworld.db`) is created on first API start with `EnsureCreated`. Switch to EF migrations before production.
- Facebook and Instagram require a public privacy policy URL. Fill in `Views/Home/Privacy.cshtml`.
- Photos (upload, gallery, profiles) are the next feature to build on top of this skeleton.
