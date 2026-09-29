# ISMLTS — guide for Claude Code

Integrated School Management and Learning Tracking System for IIE Rosebank College (WIL module XADAD7112, Group 1 ADAD1). This repo is the ASP.NET Core MVC web app for Admins, Lecturers and Students. A teammate builds the student Android app separately; it will call Web API endpoints added here later.

## Stack
- ASP.NET Core MVC, .NET 9 or later (check `TargetFramework` in the .csproj), C#
- EF Core + SQL Server: LocalDB in development, Azure SQL in production
- Cookie authentication with roles `Admin`, `Lecturer`, `Student`; passwords hashed with BCrypt.Net-Next
- Bootstrap 5.3.3 and Bootstrap Icons 1.11.3 from jsDelivr with SRI hashes; QRCoder for QR codes
- SonarQube Cloud (strict quality gate) and GitHub Actions CI

## Structure
- Namespace `ISMLTS_WebApp_`; the project folder is `ISMLTS(WebApp)/`
- `Models/` entities + view models · `Data/` ApplicationDbContext + DataSeeder · `Repositories/` IRepository<T>/Repository<T> plus one interface/class pair per entity · `Services/` business logic (RiskCalculator, AttendanceVerifier, QrCodeService) · `Extensions/` ClaimsPrincipal helpers · `ViewComponents/` ModuleSwitcher + NotificationBell
- `wwwroot/css/site.css` is global; page-specific files (`landing.css`, `landing.js`, `attendance.js`) load through `@section Styles` / `@section Scripts`

## Rules
1. Controllers never use ApplicationDbContext directly: go through a repository interface registered in Program.cs.
2. Business logic lives in `Services/`, not in controllers or views.
3. No CSS or JS inside .cshtml: no `<style>`, no `style=""`, no inline `<script>`.
4. Never bind `PasswordHash` from a form. Create actions take a separate `string password` and hash it with BCrypt; Edit actions never touch the hash; `PasswordHash` has no `[Required]`.
5. Every POST has `[ValidateAntiForgeryToken]`; model-bound POSTs have an explicit `[Bind(...)]` list.
6. Get the logged-in user with `User.GetUserId()`. Lecturers may only act on modules where `Module.LecturerId` is theirs.
7. New timestamps use `DateTime.UtcNow`; display with `.ToLocalTime()`.
8. Security-relevant random values come from `RandomNumberGenerator`, never `Random`.
9. Numbers written into HTML/JS use `CultureInfo.InvariantCulture`; the app runs `UseRequestLocalization("en-US")` so posted decimals use ".".
10. Forms: dropdowns and checkboxes instead of free text wherever the options are known.

## SonarCloud issues already fixed once (don't reintroduce)
- Every `<label>` has `for`/`asp-for` matching an input `id`; empty `<th>` cells get visually-hidden text
- `<dt>`/`<dd>` pairs sit inside one `<dl>`
- CDN `<link>`/`<script>` tags carry `integrity` and `crossorigin="anonymous"`
- No duplicate selectors within a CSS file; no commented-out code
- JS: `const`/`let`, `for...of`, `Number.parseFloat`/`Number.parseInt`, `globalThis`
- `await app.RunAsync()` in Program.cs; async EF methods throughout

## MVC gotchas we've hit
- View folders are plural and match the controller name (`Views/Admins`, not `Views/Admin`)
- A view's `@model` must match what its action passes
- `View(someString)` is treated as a view name; use `View(model: someString)`
- Navigation properties are empty unless the repository query `.Include()`s them
- After renaming view folders: Clean Solution, then Rebuild

## Database
- Package Manager Console, default project = the web app: `Add-Migration <Name>`, then `Update-Database`
- DataSeeder applies migrations and seeds only empty tables at startup; update it when the schema changes

## Status
Done: login (with returnUrl), CRUD for Students/Lecturers/Admins/Modules/Courses, enrolment, Marks with risk flag (average below 50%), Assessments with link submissions, Tickets, landing page, role dashboards, notification bell, QR attendance with campus-IP and GPS checks.
Phase 0 done: role attributes on every controller, lecturer ownership and student enrolment checks, POST logout, login rate limit (`RateLimiting:LoginAttemptsPerMinute`, default 5 per IP), duplicate/password validation, http(s)-only submission links, integration tests in `ISMLTS.Tests/Integration`.
Phase 1 done: shared page parts and toasts on every page, confirm dialog instead of Delete pages, server-side search and paging on admin lists, instant table filters, styled status pages, redesigned log-in page, button set and subtle motion.
After Phase 1: course pages to group modules and enrol a class per term (`CourseService`); `DataSeeder.SeedDataAsync` runs on every startup and keeps ADAD0701 with its 8 modules (4 per term) without duplicating anything.
The site is live on Azure App Service (published by hand). Next: see ROADMAP.md, Phases 2–9 (all approved):
## Roadmap
ROADMAP.md holds the remaining work. One phase per session: plan briefly, implement, add tests, run `dotnet build` and `dotnet test`, then stop with a manual test checklist. Tick finished items there and keep Status here current.

## Migrations from Claude Code
Use `dotnet ef migrations add <Name> --project "ISMLTS(WebApp)"` if the dotnet-ef tool is installed. If it isn't, stop and tell me the migration name to run in Package Manager Console.

## Security baseline
- Every controller carries `[Authorize]`. Role attributes on a class and an action combine with AND, so a controller serving two roles puts plain `[Authorize]` on the class and roles on each action.
- Never trust ids from routes or forms: re-check ownership (lecturer → module, student → own records) in every action.

## UI conventions
- Pages use the `_PageHeader` and `_EmptyState` partials and `panel` sections; forms sit in `panel form-panel` with a `form-actions` row (primary `btn-rosebank`, Cancel `btn-outline-secondary`).
- In `model='…'` partial attributes, don't put an apostrophe inside the C# strings: it ends the attribute.
- After a create/edit/delete/submit, call `this.Toast("…")` (sets `TempData["Toast"]` and `TempData["ToastType"]`: success, danger or info) before redirecting.
- Submit buttons get `data-loading`; destructive actions are POST forms with `data-confirm="…"` (optional `data-confirm-label`). There are no separate Delete pages.
- Row actions are `btn btn-icon` with `data-bs-toggle="tooltip"`, `data-bs-title` and an `aria-label`. Tables sit in `.table-scroll` (sticky header, sideways scroll on phones).
- Admin lists page on the server (`SearchAsync(q, page)` → `PagedList<T>`, `_ListSearch` + `_Pager`); other tables use `_TableFilter` (`data-table-filter`) with a `data-filter-empty` message.
- Nav links use `highlight-active`; empty 4xx/5xx responses show `_StatusMessage` via `/Status/{code}`.
- Motion stays subtle and switches off under `prefers-reduced-motion`. No `filter: blur` or `backdrop-filter`: the lab PCs are VMs without GPU acceleration and scrolling stutters.
- No new CDN libraries (each needs an SRI hash). Charts use `<progress>`, CSS or server-rendered SVG.

## Testing
xUnit project `ISMLTS.Tests` is in the solution and CI runs it on every push. Services get unit tests; security rules get WebApplicationFactory integration tests (environment `Testing`, SQLite in-memory + `EnsureCreated`, DataSeeder skipped).

## Deployment
Azure App Service (Windows) + Azure SQL. The connection string, `Seed__AdminPassword` and `Seed__LecturerPassword` live in App Service settings, never in the repo. `UseSqlServer` keeps `EnableRetryOnFailure()` because Azure SQL can pause and resume.
xUnit tests for Services, Azure deployment, Web API endpoints for the Android app, file-upload submissions (Azure Blob).
Known debt: rows saved before Phase 0 stored local time in `Submission.SubmittedAt` and `Ticket.DateOpened`/`DateResolved`, so they now display 2 hours late. The student dashboard's announcements are placeholders until Phase 2.

## CI and commits
- `.github/workflows/dotnet.yml`: `dotnet-version` must match the .csproj `TargetFramework`; `working-directory` must point at the folder holding the .sln
- One feature per commit with a specific imperative message ("Add QR attendance sessions"); commit quality is marked

## Definition of done
Build with no new warnings, run the app, and click through the feature as every role that uses it.