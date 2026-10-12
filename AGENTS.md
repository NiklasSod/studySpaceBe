# AGENTS.md — Project guide for AI assistants

Read this first before making changes. It captures the context that would otherwise
require re-exploring the repository.

## What this is

`studySpaceBe` (API project: `lmsPortalBe`) — ASP.NET Core backend for an LMS
portal. Students enroll in courses, teachers manage content/assignments, admins
manage roles. Frontend is a separate SPA.

## Stack

- .NET 10 (`net10.0`), ASP.NET Core Web API (attribute-routed controllers, not minimal APIs)
- EF Core + SQLite (`journal_mode=WAL`); migrations auto-applied on startup
- ASP.NET Core Identity (`ApplicationUser : IdentityUser`) with roles `student` / `teacher` / `admin`
- JWT (Bearer) access token + HttpOnly refresh-token cookie (rotation + reuse detection)
- AutoMapper, HtmlSanitizer, Cloudinary (voice notes), Vercel Blob (interactive images), Resend (email)
- Config via `.env` (DotNetEnv, loaded in Program.cs) + appsettings; deployed on Railway

## Commands

```bash
dotnet build src/lmsPortalBe/lmsPortalBe.csproj
dotnet run --project src/lmsPortalBe          # listens on http://localhost:5250
dotnet test tests/lmsPortalBe.Tests/lmsPortalBe.Tests.csproj
dotnet ef migrations add <Name> --project src/lmsPortalBe
```

## Layout

- `src/lmsPortalBe/Controllers` — one controller per domain; `[ApiController]` + `[Route("api/[controller]")]`
- `src/lmsPortalBe/DTOs/<Area>/` — request/response DTOs with DataAnnotations validation
- `src/lmsPortalBe/Models` — EF entities + enums
- `src/lmsPortalBe/Data` — `LmsPortalContext` (+ `ILmsPortalContext` abstraction), `DbSeeder`, design-time factory
- `src/lmsPortalBe/Services` — interface + implementation per service (Token, Notification, Email, RichTextSanitizer, Cloudinary, VercelBlob, RefreshTokenCleanup)
- `src/lmsPortalBe/MappingProfiles` — `AutoMapperProfile`
- `tests/lmsPortalBe.Tests` — xUnit integration tests (`WebApplicationFactory` + in-memory SQLite)

## Auth & roles

- `AuthController` (anonymous): `register`, `login`, `refresh`, `logout`, `forgot-password`, `reset-password`
- `AccountController` `[Authorize]`: update self, `change-password`, delete self
- `AdminController` `[Authorize(Roles="admin")]`: `assign-role`, delete course
- `student` and `teacher` are mutually exclusive; an admin's role cannot be reassigned
- Password policy: min 8 chars, at least one lower, one upper, one digit
- Password reset: token via `UserManager.GeneratePasswordResetTokenAsync`, email via `IEmailSender`
  (Resend). Reset revokes all refresh tokens. `forgot-password` is rate-limited 5/10min/IP and
  always returns `204` (no account enumeration).

## Config keys (see `.env.example`)

- `JWT_SECRET` (required), `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_ACCESS_TOKEN_MINUTES=30`, `JWT_REFRESH_TOKEN_DAYS=7`
- `ConnectionStrings__DefaultConnection=Data Source=lmsPortalBe.db;Foreign Keys=True`
- `CORS_ALLOWED_ORIGINS` (comma-separated; overrides `Cors:AllowedOrigins`)
- `ADMIN_USERNAME` / `ADMIN_EMAIL` / `ADMIN_PASSWORD` (seed admin on startup)
- `RESEND_API_KEY` + `EMAIL_FROM` (password-reset email), optional `PASSWORD_RESET_PAGE_URL`
- `BLOB_READ_WRITE_TOKEN` (Vercel Blob), `CLOUDINARY_CLOUD_NAME` / `_API_KEY` / `_API_SECRET`

## Deployment (Railway)

- Production runs on Railway. The database is **SQLite stored on a Railway volume** — the same
  provider as local, so there is no provider switch; migrations auto-apply and WAL mode is used.
- Configuration comes from **Railway environment variables** in production (there is no `.env`
  file); locally the same keys come from `.env`. See "Config keys" above for the full list. Only
  names are documented — values are never committed.
- Treat the env-var names as a production contract: do not rename or remove one without updating its
  Railway variable and every reference. The secret-only keys — `JWT_SECRET`,
  `ConnectionStrings__DefaultConnection` (points at the volume DB file), `ADMIN_PASSWORD`,
  `RESEND_API_KEY`, `BLOB_READ_WRITE_TOKEN`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET` — live
  only on Railway and in the local `.env`, never in a file under version control.
- Railway injects `PORT`; Program.cs binds `http://0.0.0.0:PORT` when present.
- Forwarded headers (`X-Forwarded-For` / `X-Forwarded-Proto`) are trusted so HTTPS redirection works
  behind Railway's edge.

## Testing notes

- `TestWebApplicationFactory` boots the real app against a shared in-memory SQLite DB and sets
  JWT/admin env vars; it replaces `IEmailSender` with `RecordingEmailSender` so tests can assert
  on sent reset links.
- `ApiTestBase` has helpers: `RegisterAsync`, `LoginAsync`, `RegisterWithTokensAsync`,
  `EnrollAndApproveAsync`, `SendAuthorizedAsync`.
- xUnit v3 with `mtp-off` → VSTest runner; filter with `--filter "FullyQualifiedName~ClassName"`.

## Conventions & gotchas

- Controllers use primary-constructor DI; services use classic constructor injection.
- External services resolve credentials lazily (`Lazy<>`) so the app boots without API keys and
  only throws when actually used (see `CloudinaryAudioService`, `ResendEmailSender`).
- `.env` is gitignored — document any new env var in `.env.example` and `README.md`.
