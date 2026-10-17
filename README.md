# PhotoWorld

A portal for sharing a passion for photography with people around the world.
ASP.NET Core MVC on **.NET 10**, split into an API, a UI and a shared library.

```
PhotoWorld.slnx
├── src/PhotoWorld.Shared   Class library: DTOs, API routes, provider names, claim types, Supabase logger (used by both)
├── src/PhotoWorld.Api      Web API: users, photos, likes, comments (Supabase Postgres + Storage), JWT issuing
├── src/PhotoWorld.Web      MVC UI: members-only pages, cookie auth, Google sign-in, gallery and upload
└── tests/PhotoWorld.Api.Tests  xUnit tests for image processing
```

## How sign-in works

1. The user clicks **Continue with Google** on `/Account/Login` (or `/Account/Register`).
2. **Web** runs the OAuth flow with the provider and receives the provider's access token.
3. **Web** sends it to **API** `POST /api/auth/external`. The API checks the token **directly with Google**
   (`tokeninfo`), confirming it was issued to PhotoWorld.
4. The API finds or **registers** the user (first sign-in = registration). An account with the same verified
   email from another provider is linked, so it doesn't create a duplicate. It then returns a PhotoWorld JWT.
5. **Web** signs the user in with a cookie that carries the JWT and attaches it to later API calls (`GET /api/users/me`).

## Supabase (database + logs)

All data lives in a Supabase Postgres database, in a dedicated **`photoworld`** schema. It's kept out of
`public` on purpose: Supabase's auto-generated Data API exposes `public` tables to anyone holding the anon key.

| Table | Written by | Contents |
|---|---|---|
| `photoworld.users`, `photoworld.external_logins` | API (EF Core) | Members and their linked Google accounts |
| `photoworld.photos`, `photo_likes`, `photo_comments` | API (EF Core) | Photo details (title, camera settings, file paths), likes and comments |
| `photoworld.blogs`, `blog_posts`, `blog_post_photos`, `blog_comments` | API (EF Core) | Members' blogs, posts (sanitized HTML), the photos each post embeds, and comments |
| `photoworld.reports`, view `open_reports` | API (EF Core) | Members' reports about photos, comments, blog posts and blog comments, and the moderation queue |
| Storage bucket `photos` (public) | API | The photo files: `{userId}/{photoId}/display.jpg` and `thumb.jpg` |
| `photoworld.app_logs` | API **and** Web (`SupabaseLoggerProvider`) | Application logs, tagged with `application` = `PhotoWorld.Api` / `PhotoWorld.Web` |
| `photoworld.__ef_migrations_history` | API | Applied EF migrations |

**1. Connect the API.** In the Supabase dashboard, click **Connect** and copy the **Session pooler**
connection string in .NET format. (The direct connection is IPv6-only on the free plan; the transaction
pooler doesn't support the bulk COPY the logger uses.)

```bash
dotnet user-secrets set "ConnectionStrings:PhotoWorld" "User Id=postgres.<project-ref>;Password=<db-password>;Server=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;SSL Mode=Require" --project src/PhotoWorld.Api
```

Create the schema and tables by applying the EF migrations (`dotnet ef` picks up the connection string
from the API's user-secrets):

```bash
dotnet tool restore
dotnet ef database update --project src/PhotoWorld.Api
```

Or run [`docs/supabase-schema.sql`](docs/supabase-schema.sql) in the SQL editor. The API doesn't migrate on
startup. If the database is behind the code, it logs a warning listing the missing migrations.

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
A nightly `pg_cron` job (03:15 UTC, created by the `LogRetention` migration) deletes log rows older than
30 days. Check it with `select * from cron.job;`.

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

New EF migrations: `dotnet ef migrations add <Name> --project src/PhotoWorld.Api --output-dir Data/Migrations`.

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) builds every push and pull request. On `main`, once the
build is green, it applies any pending EF migrations to Supabase. It needs one repository secret,
`SUPABASE_DB_CONNECTION_STRING`, holding the Session pooler connection string for the `postgres` user
(GitHub → Settings → Secrets and variables → Actions).

## Photos

The whole site is members-only: everything except the landing, sign-in and privacy pages needs a Google
sign-in. Members can upload photos, browse the gallery, like and comment, and edit or delete their own photos.

**Upload pipeline** (`ImageProcessor` in the API): accepts JPEG, PNG, WebP or iPhone HEIC up to 25 MB. HEIC is
decoded with Magick.NET (libheif) into an upright, metadata-free image first. It reads the camera settings from EXIF and stores them in `photoworld.photos`, then re-encodes
the image as two JPEGs: `display` (longest side ≤ 2560 px) and `thumb` (≤ 640 px). The original is never kept,
and the re-encoding drops all metadata, so GPS locations never reach the public bucket.

**Photo storage setup.** The `AddPhotos` migration creates the public `photos` bucket. The API uploads to it
with your project's secret key (Supabase dashboard → Project Settings → API Keys → *Secret keys*, `sb_secret_…`;
the legacy `service_role` key also works). The key stays in the API; the UI never sees it.

```bash
dotnet user-secrets set "Supabase:SecretKey" "<sb_secret_…>" --project src/PhotoWorld.Api
```

`Supabase:Url` is in the API's `appsettings.json`. Image URLs are public: pages need sign-in, but anyone who
has a direct image link can open it.

Run the tests with `dotnet test`.

**Deleting an account.** Members can delete their account from *Account settings* (linked from their member page).
After typing `DELETE`, the API removes the user row, which cascades to their photos, likes, comments, reports
and linked Google sign-in, and then deletes their files from the bucket. Log entries keep only the user id and
expire after 30 days.

## Home-page slideshow

The home page is the one public page with member content: a slideshow of photos their owners chose to feature.
Members tick **Show on the PhotoWorld home page** when uploading or editing a photo (off by default). The
public `GET api/showcase` endpoint returns the 12 newest featured photos (title, image and owner name),
leaving out anything hidden by reports. The website caches it for 60 seconds.

## Blogs

Every member has a blog (**Blogs** in the menu, or *Blog* on a member page) with a title and intro, set under
*Blog settings*. Posts are written in a Quill 2 editor (`wwwroot/lib/quill`) with headings, bold, italic,
lists, quotes, links and a **photo** button. That button picks from your gallery or uploads a new photo, which
also lands in your gallery. Posts can be saved as drafts (visible only to you) or published. Members comment on
published posts; comment authors and post authors can delete comments.

**Safety.** The API never trusts post HTML. `BlogHtml` runs every body through HtmlSanitizer, which keeps
only basic formatting, http(s)/mailto links (opened with `rel="nofollow noopener"`) and the author's own
photos, re-pointed at their real storage URL. Scripts, styles, event handlers and foreign images are removed.
When a post is shown, photos that were since deleted or hidden by moderation are dropped from it. The first
photo is the post's cover on cards.

## Reports and moderation

Members can report a photo, a photo comment, a blog post or a blog comment (spam, nudity, harassment, violence,
copyright, other), once per item, but not their own. Content is **hidden** from everyone except its owner when:

- it has **3 or more open reports** from different members (pending review), or
- a moderator marked one of its reports **upheld** (removed).

Owners see a banner ("Hidden while we review reports" / "Removed by a moderator") and can still delete it.

There's no admin page: you moderate in the Supabase dashboard.

1. **Table Editor → `photoworld` schema → `open_reports` view**: open reports, newest first, with the reported
   photo title or comment text, its owner, the reporter, the reason and how many open reports the item has.
2. Act on it in the **`reports`** table (or the SQL editor). `reviewed_at` is filled in automatically.

```sql
-- Remove the content: uphold its open reports
update photoworld.reports set status = 'upheld' where photo_id = '<photo id>' and status = 'open';

-- Nothing wrong: dismiss the reports (auto-hidden content reappears)
update photoworld.reports set status = 'dismissed' where comment_id = '<comment id>' and status = 'open';

-- Blog posts and blog comments work the same way, with blog_post_id / blog_comment_id
update photoworld.reports set status = 'upheld' where blog_post_id = '<post id>' and status = 'open';
```

Upheld photos stay in the bucket until their owner deletes them or their account. Every report and every
auto-hide is logged in `app_logs` (search for "reported for" and "auto-hidden").

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

## Configure Google sign-in

Credentials go in **user-secrets** (never in `appsettings.json`). Web needs ClientId + ClientSecret.
The API needs the same ID to check that tokens were issued to PhotoWorld.

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → Create OAuth client ID → *Web application*.
2. Authorized redirect URI: `https://localhost:7211/signin-google`
```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>" --project src/PhotoWorld.Web
dotnet user-secrets set "Authentication:Google:ClientId" "<id>" --project src/PhotoWorld.Api
```

## Notes / next steps
- The privacy policy is at `/Home/Privacy`. Set `Site:ContactEmail` so it shows a contact address, and use
  that URL for the Google consent screen. Have it reviewed before going live.
- Possible next steps: an in-app moderation page, email notifications for new reports.
