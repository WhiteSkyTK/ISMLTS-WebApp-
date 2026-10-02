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
5. Every POST has `[ValidateAntiForgeryToken]`; model-bound POSTs have an explicit `[Bind(...)]` list. Exception: the `/api/v1` controllers (Controllers/Api) use bearer tokens, so they take small request records instead and need no antiforgery token.
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
- DemoSeeder (`Seed:DemoData` + a `Seed:DemoPassword` of 8+ characters; on in appsettings.Development.json) adds 3 courses, 4 lecturers, 30 students, marks, attendance and tickets once per database (the DSWD0601 course marks it done), also next to real data on the live site: existing emails, module codes and attendance codes are skipped. To start over locally: PMC `Drop-Database`, then run the app.
- Student emails must end in `@rcconnect.edu.za` (`Students:EmailDomain`, checked in StudentsController).

## Status
Done: login (with returnUrl), CRUD for Students/Lecturers/Admins/Modules/Courses, enrolment, Marks with risk flag (average below 50%), Assessments with link submissions, Tickets, landing page, role dashboards, notification bell, QR attendance with campus-IP and GPS checks.
Phase 0 done: role attributes on every controller, lecturer ownership and student enrolment checks, POST logout, login rate limit (`RateLimiting:LoginAttemptsPerMinute`, default 5 per IP), duplicate/password validation, http(s)-only submission links, integration tests in `ISMLTS.Tests/Integration`.
Phase 1 done: shared page parts and toasts on every page, confirm dialog instead of Delete pages, server-side search and paging on admin lists, instant table filters, styled status pages, redesigned log-in page, button set and subtle motion.
After Phase 1: course pages to group modules and enrol a class per term (`CourseService`); `DataSeeder.SeedDataAsync` runs on every startup and keeps ADAD0701 with its 8 modules (4 per term) without duplicating anything.
Phase 2 done: notifications (bell with unread count, list, settings), announcements, ticket emails and a Monday "due this week" digest via Azure Communication Services. Needs the `Phase2Notifications` migration.
Phase 3 done: marks link to assessments with feedback and a release switch (students only see released marks), a mark history on every mark (MarkService / MarkChange), gradebook, Quick Eval, CSV import with preview (GradingController), My Progress and Class Insights (InsightsController, `Risk:AttendanceThreshold`, default 75), CSV exports of marks and registers (Csv escapes formulas). Needs the `Phase3Marking` migration.
After Phase 3: development demo data, the markbook (Marks/ForModule: students × assessments with Release marks / Hide marks cards), sortable and filterable tables everywhere, and SonarCloud accessibility fixes.
Phase 4 done: Profile (change password), authenticator two-factor sign-in (TwoFactor service, Otp.NET; required for admins via `TwoFactor:RequiredForAdmins`), admin password resets and two-factor switch-off (UserSecurityController), My Portfolio, Awards, Help, the POPIA privacy notice, `ExternalLinks` shortcuts, CSV bulk import of students and lecturers (ImportController), and admin Reports. Needs the `Phase4TwoFactor` migration.
Phase 5 done: the student API for the Android app under `/api/v1` (Controllers/Api; JWT access tokens + single-use refresh tokens via ApiTokenService, `Jwt` settings; Swagger UI at /swagger in Development; guide in `docs/API.md`). Student actions shared by the website and the API live in `StudentPortalService`. Needs the `Phase5ApiTokens` migration.
Phase 6 (code) done: `/health` database check, `ForwardedHeaders:Enabled` switch, admin Site check page (SiteCheckService), Application Insights through Azure Monitor OpenTelemetry, demo data addable once to the live site, CI with OpenCover coverage, SonarCloud (with `SONAR_TOKEN`) and an App Service deploy job (with the Azure variables and secrets), build output no longer tracked, README go-live runbook. Still to do on Azure: publish, set the settings, run Site check and the test checklists live.
Phase 7 done: file submissions (PDF/DOCX/ZIP checked by content in `SubmissionFileRules`; `IFileStore` = BlobFileStore with short-lived SAS links when `Storage:ConnectionString` is set, otherwise LocalFileStore in `App_Data/submissions`; `SubmissionFileService` checks the student or module lecturer before any download), upload history (`SubmissionFile`, newest counts), per-assessment late window (`Assessment.LateDays`, `SubmissionWindow`), API upload/download, Site check storage line. Needs the `Phase7Files` migration.
Phase 8 done: terms (Admin → Terms, `TermService`; the latest started term is current and drives the dashboard's Current/Archived courses), attendance counted per module term with cancelled classes left out (`AttendancePeriods` passed to the attendance repository queries), lecturer timetables (`TimetableSlot`, Attendance shows "Take register" for the class on now), and a Calendar page for every role plus a private `.ics` feed link for students (`CalendarService`, token stored as SHA-256 and shown once). With no terms set up, everything counts as before. Needs the `Phase8Terms` migration. Then: month calendar with college dates (`CollegeDate`, managed on the Terms page), closing dates, extra classes (`TimetableSlot.OnDate`), personal notes and to-dos (`CalendarNote`, reminders sent to the bell by `NoteReminderService`), and `DemoSeeder.SeedCalendarAsync` (terms, timetables, college dates while those tables are empty). Needs the `Phase8Calendar` migration.
The site is live on Azure App Service (Phases 0–6 published). Next: see ROADMAP.md, Phase 9 (approved), plus the live checks left in Phases 6–8:
## Roadmap
ROADMAP.md holds the remaining work. One phase per session: plan briefly, implement, add tests, run `dotnet build` and `dotnet test`, then stop with a manual test checklist. Tick finished items there and keep Status here current.

## Migrations from Claude Code
Use `dotnet ef migrations add <Name> --project "ISMLTS(WebApp)"` if the dotnet-ef tool is installed. If it isn't, stop and tell me the migration name to run in Package Manager Console.

## Security baseline
- Every controller carries `[Authorize]`. Role attributes on a class and an action combine with AND, so a controller serving two roles puts plain `[Authorize]` on the class and roles on each action.
- Never trust ids from routes or forms: re-check ownership (lecturer → module, student → own records) in every action.
- Sign-in, passwords and two-factor go through `IAccountService` (users live in three tables; `UserAccount` wraps whichever one). The session's claims come from `AccountService.Principal`; `AccountSetupFilter` keeps people on their profile while a temporary password must be changed or an admin's session hasn't passed the authenticator step.
- Never show a password or recovery code twice: temporary passwords and recovery codes appear once, on the page that creates them. Store only BCrypt hashes (passwords) or SHA-256 hashes (recovery codes).

## UI conventions
- Pages use the `_PageHeader` and `_EmptyState` partials and `panel` sections; forms sit in `panel form-panel` with a `form-actions` row (primary `btn-rosebank`, Cancel `btn-outline-secondary`).
- In `model='…'` partial attributes, don't put an apostrophe inside the C# strings: it ends the attribute.
- After a create/edit/delete/submit, call `this.Toast("…")` (sets `TempData["Toast"]` and `TempData["ToastType"]`: success, danger or info) before redirecting.
- Submit buttons get `data-loading`; destructive actions are POST forms with `data-confirm="…"` (optional `data-confirm-label`; `data-confirm-tone="primary"` for non-destructive questions such as releasing marks). There are no separate Delete pages.
- Row actions are `btn btn-icon` with `data-bs-toggle="tooltip"` and `data-bs-title`; icon-only links and buttons are named by visually-hidden text inside them (`<span class="visually-hidden">Edit Thandi</span>`), not an `aria-label`, because SonarCloud wants anchors to have readable content. Tables sit in `.table-scroll` (sticky header, sideways scroll on phones).
- Admin lists page and sort on the server (`SearchAsync(q, page, sort, filters)` → `PagedList<T>` with `Sort`; `ListSort.Parse` only accepts the repository's own keys; headers use `_SortHeader`, filters go in `ViewData["ListFilters"]` for `_ListSearch`, then `_Pager`). Other tables use `_TableFilter` (`data-table-filter`, optional "Show" dropdowns via `ViewData["TableFacets"]` matching `data-{name}` tokens on rows) with a `data-filter-empty` message, and `table[data-sortable]` + `th[data-sort="text|number"]` (`data-sort-value` on cells) for client-side sorting.
- Release marks with the `_ReleaseButton` partial (pass `ReturnUrl` to come back to the same page).
- Accessibility (SonarCloud): no `autofocus`; prefer real elements over roles (`<button>` not `role="button"`, hidden text not `role="img"`, no `role="status"`/`"switch"`); buttons are named by their visible text plus visually-hidden text rather than an `aria-label` that differs from it; text/background pairs in one CSS rule use solid colours that pass 4.5:1.
- Nav links use `highlight-active`; empty 4xx/5xx responses show `_StatusMessage` via `/Status/{code}`.
- Motion stays subtle and switches off under `prefers-reduced-motion`. No `filter: blur` or `backdrop-filter`: the lab PCs are VMs without GPU acceleration and scrolling stutters.
- No new CDN libraries (each needs an SRI hash). Charts use `<progress>`, CSS or server-rendered SVG.

## Testing
xUnit project `ISMLTS.Tests` is in the solution and CI runs it on every push. Services get unit tests; security rules get WebApplicationFactory integration tests (environment `Testing`, SQLite in-memory + `EnsureCreated`, DataSeeder skipped).

## Deployment
Azure App Service (Windows) + Azure SQL. The connection string, `Seed__AdminPassword` and `Seed__LecturerPassword` live in App Service settings, never in the repo. Email is optional: set `Email__ConnectionString` (Azure Communication Services), `Email__From` (a verified sender address) and `Email__SiteUrl` (the site's https address, used in email links); without them nothing is sent. The weekly digest runs in the app, so the App Service needs Always On. `Jwt__SigningKey` (a random secret of 32+ characters) signs the app's tokens; without it they stop working whenever the site restarts. Optional: `ExternalLinks__IieLibrary` and `ExternalLinks__StudentPortal` (https addresses; each shortcut is hidden while empty). `TwoFactor__RequiredForAdmins` defaults to true. Also set `WEBSITE_TIME_ZONE` = `South Africa Standard Time` (otherwise the server runs on UTC), HTTPS Only, Always On and the health check path `/health`; `ForwardedHeaders__Enabled` = true only if Site check flags the proxy's address; `APPLICATIONINSIGHTS_CONNECTION_STRING` optional; `Seed__DemoData` + `Seed__DemoPassword` add the demo data once for live testing. Uploads need `Storage__ConnectionString` (an Azure Storage account; the private `submissions` container is created on first upload); `Submissions__MaxFileMegabytes` defaults to 20 (max 25, IIS limit). After every deploy an admin opens Site check. The deploy job needs the repository variable `AZURE_WEBAPP_NAME` (optional `AZURE_WEBAPP_SLOT`) and the secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`; README.md has the full runbook. `UseSqlServer` keeps `EnableRetryOnFailure()` because Azure SQL can pause and resume.
Known debt: rows saved before Phase 0 stored local time in `Submission.SubmittedAt` and `Ticket.DateOpened`/`DateResolved`, so they now display 2 hours late.

## CI and commits
- `.github/workflows/dotnet.yml`: `dotnet-version` must match the .csproj `TargetFramework`; `working-directory` must point at the folder holding the .sln
- One feature per commit with a specific imperative message ("Add QR attendance sessions"); commit quality is marked

## Definition of done
Build with no new warnings, run the app, and click through the feature as every role that uses it.