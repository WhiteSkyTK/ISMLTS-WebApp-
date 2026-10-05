# ISMLTS roadmap

One phase per session: plan briefly, implement, add tests, run `dotnet build` and `dotnet test`, then stop with a manual test checklist. Tick items here and keep the Status section of CLAUDE.md current. Commit per feature.

## Phase 0 — Lock down before going live
- [x] `[Authorize(Roles = "Admin")]` on StudentsController, LecturersController, AdminsController, ModulesController, CoursesController
- [x] Marks, Assessments, Tickets, Attendance: plain `[Authorize]` on the class, role attributes on each action (roles on class + action combine with AND)
- [x] Lecturer ownership checks in Marks, Assessments and Tickets, matching AttendanceController.GetOwnedModuleAsync
- [x] Replace every hand-written claims-parsing snippet with `User.GetUserId()`; stored timestamps use `DateTime.UtcNow`
- [x] Logout becomes a POST form with an antiforgery token
- [x] Rate-limit the login POST (built-in rate limiter, about 5 attempts per minute per IP) with a friendly message
- [x] Duplicate emails, usernames and codes show a validation message instead of crashing (check first, also catch DbUpdateException); passwords need at least 8 characters
- [x] Integration tests (WebApplicationFactory, environment `Testing`, SQLite in-memory + EnsureCreated, DataSeeder skipped): anonymous users go to login, students get 403 on admin and lecturer pages, a lecturer can't open another lecturer's module

## Phase 1 — Everyday polish
- [x] Shared partials `_PageHeader` (title, subtitle, back link, primary action) and `_EmptyState`, used on every page
- [x] Toasts: the layout renders `TempData["Toast"]` as an auto-hiding Bootstrap toast; every create/edit/delete/submit sets one
- [x] `data-loading` on submit buttons (disable + spinner, no double posts); `data-confirm` deletes through one shared modal; retire the separate Delete pages
- [x] Row actions become small icon buttons with tooltips; tables get a search box (`data-table-filter`), sticky headers and horizontal scroll on mobile; admin lists paginate past 25 rows
- [x] Nav highlights the active link; admin nav gains Courses and Admins; the icon row becomes role-specific (pointing at Phase 3/4 pages); remove Language from the avatar menu
- [x] Styled 404, 403 and error pages via `UseStatusCodePagesWithReExecute`
- [x] Subtle motion (page fade-in, card hover lift, unread-badge pulse), all off under `prefers-reduced-motion`
- [x] Remove every remaining inline `style=""` from views
- [x] Log-in page redesign (brand panel, input icons, show/hide password, Caps Lock hint) and one button style set (`btn-rosebank`, `btn-outline-rosebank`, `btn-icon`)
- [x] Courses: tick a course's modules and enrol a class in all of them (per term) at once; the seeder keeps ADAD0701 with its 8 modules (4 per term)

## Phase 2 — Notifications and announcements
- [x] Notification entity (UserId, Role, Title, Message, Url, CreatedAt, IsRead) with repository and INotificationService
- [x] Triggers: assessment posted → enrolled students; mark captured/updated → that student; ticket raised → module lecturer; ticket answered → student; attendance session started → enrolled students ("Attendance is open for XADAD7112 — scan the QR in class", never include the code); lecturer marks a student present or removes a scan → that student; announcement posted → its audience
- [x] Bell: unread count, latest 8, clicking marks read and follows the link (local URLs only), "Mark all read", "View all" page with All/Unread filter; the "due soon" list stays as a second section
- [x] Announcement entity: lecturers post to their modules, admins post to everyone; the dashboard Announcements tab uses real data
- [x] Email for ticket replies (both directions) and a weekly "due this week" digest for students (Azure Communication Services), with an opt-out per user on a Notification settings page
- [x] Unit tests for NotificationService

## Phase 3 — Marking and progress
- [x] Marks link to assessments: nullable `Mark.AssessmentId` + `Mark.Feedback`
- [x] Quick Eval (lecturer): queue of submitted-but-unmarked work across their modules, oldest due first, inline score/max/feedback with "Save & next"
- [x] Gradebook: per assessment, every enrolled student in one grid, save all at once with per-row validation
- [x] Students see marks and feedback on My Assessments and My Marks
- [x] My Progress (student, the "Insights" icon): per-module average, risk badge, attendance %, submitted x of y, next due, with an overall summary on top
- [x] Class Insights (lecturer): per-module class average, attendance rate, submission rate, and an at-risk list (average below 50% or attendance below `Risk:AttendanceThreshold`, default 75) with quick actions
- [x] CSV export of module marks and session registers (values escaped properly)
- [x] Audit log of mark changes (who, when, old → new) shown on the mark
- [x] Lecturers release marks per assessment; students don't see a mark before it is released
- [x] CSV import of marks for a whole assessment, with a preview and per-row errors
- [x] Unit tests for every new calculation

## After Phase 3 — Testing feedback
- [x] Demo data in Development (`Seed:DemoData`, `Seed:DemoPassword`): 3 courses, 16 modules, 4 lecturers, 30 students, assessments, submissions, marks, attendance, tickets, announcements; only fills a database with no students
- [x] Student emails must end in `@rcconnect.edu.za` (`Students:EmailDomain`)
- [x] Markbook: students × assessments grid with Release marks / Hide marks cards; the same buttons on the assessments list and the gradebook; Delete on the Edit Mark page
- [x] Sortable columns and "Show" filters on lecturer, student and enrolment tables; server-side sort and filters on the admin lists
- [x] SonarCloud accessibility and contrast fixes (real buttons for the bell and account menu, no autofocus, no `role="img"`/`"status"`/`"switch"`)

## Phase 4 — Pages behind every link
- [x] Profile for all roles: details + change password (current password required)
- [x] Two-factor sign-in with an authenticator app (TOTP via Otp.NET, QR via QRCoder): optional for everyone, required for Admins
- [x] My Portfolio (student): all submissions with marks and feedback, grouped by module
- [x] Awards (student): badges computed from existing data (95%+ attendance, top mark in an assessment, every submission on time this term)
- [x] Help: FAQ per role, ending with "Still stuck? Raise a ticket"
- [x] Privacy: POPIA notice covering marks, submissions, and the IP and GPS captured at attendance scans (why, who sees it, how long it's kept)
- [x] IIE Library and Student Portal: URLs from an `ExternalLinks` config section, opening in a new tab; hide an icon when its URL is empty
- [x] Admin: CSV bulk import of students and lecturers with a preview and per-row errors before saving; reset a user's password; Reports page (users, at-risk by module, attendance by module)

## Phase 5 — API for the Android app
- [x] `/api/v1` controllers with JWT bearer auth: login, my modules, marks, assessments, submit, tickets, notifications, attendance scan (same AttendanceVerifier)
- [x] Swagger UI in Development only; share the endpoint list with the Android teammate
- [x] Integration tests for the API auth rules
- [x] `docs/API.md`: how the Android app logs in, refreshes tokens, reads errors and calls each endpoint, with Kotlin (Retrofit/OkHttp) examples and how to reach a PC's API from the emulator or a phone

## Phase 6 — Go live on Azure
This is where everything built since Phase 1 reaches the live site and gets tested there instead of on localhost.
- [x] The App Service site is created and running (published by hand; Phases 0–1 and later changes still to be published)
- [ ] Publish Phases 2–5 to the live site: apply the migrations to Azure SQL, add the new App Service settings (`Jwt__SigningKey`, `ExternalLinks__*`, email if wanted), then work through each phase's manual test checklist on the live URL as every role, and point the Android app at the live API
- [x] GitHub Actions deploys to App Service after the tests pass (OIDC login, no publish profile in the repo): to the slot in `AZURE_WEBAPP_SLOT` (swap to production by hand) or straight to production on plans without slots; switches on once the Azure variables and secrets exist
- [x] `/health` endpoint that checks the database; the App Service health check uses it
- [x] Confirm on Azure that `RemoteIpAddress` is the student's IP (the login rate limit and the campus-network check both rely on it): the `ForwardedHeaders:Enabled` switch and the Site check page are in place; open Site check on the live site and switch it on if it flags the proxy
- [x] Application Insights for errors and slow requests, with no personal data in log messages (Azure Monitor OpenTelemetry, on when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set)
- [x] `.gitignore` covers `ISMLTS(WebApp)/bin`, `obj` and the test project's output; the tracked build output is removed from git
- [x] SonarCloud receives test coverage from CI (coverlet, OpenCover format), once the `SONAR_TOKEN` secret is added and Automatic Analysis is switched off
- [x] Deployment runbook in README: required settings, first-run seeding, rollback
- [x] Admin Site check page: environment, https, time zone, database and migrations, the address the site sees, campus network, and which settings are missing
- [x] Demo data can be added once to the live site (`Seed__DemoData`, `Seed__DemoPassword`), next to existing data, for live testing
- [x] Optional "Sign in with Microsoft" (OpenID Connect with Microsoft Entra ID, `Microsoft.AspNetCore.Authentication.OpenIdConnect`): the button only shows when `Authentication:Microsoft` is configured; it signs in an existing student or lecturer whose college email matches the verified Microsoft account and never creates accounts; password log-in stays. Check early whether the IIE tenant needs IT to approve (admin consent) the app registration (done in Phase 9; set it up with README step 3)

## Phase 7 — File submissions
- [x] Students can upload a file (PDF, DOCX, ZIP; size limit from config) as well as, or instead of, a link; files go to a private Azure Blob container (Azurite locally), never under wwwroot
- [x] Lecturers download through short-lived SAS links, after the usual module ownership check
- [x] File type checked from the file's content, not only its extension; stored names are generated, not taken from the upload
- [x] Per assessment, the lecturer can close submissions at the due date or allow a late window
- [x] Earlier uploads are kept as history; the latest one counts
- [x] Unit tests for the file checks; integration tests that nobody else can download a student's file
- [x] The Android app uploads and downloads through `/api/v1` (`docs/API.md`); Site check shows where files are stored; files go when their student, module or assessment is deleted
- [x] On the live site: create the Storage account, set `Storage__ConnectionString`, publish with the `Phase7Files` migration and try an upload and a download as a student and a lecturer

## Phase 8 — Terms and timetable
- [x] Term entity with start and end dates; "current term" drives the dashboards and the Archived filter (replaces the hard-coded `IsCurrentSemester = true`)
- [x] Timetable slots per module (day, time, venue); "Take register" goes straight to the class happening now
- [x] Attendance percentages count only the current term's sessions; cancelled classes can be excluded
- [x] Calendar page for each role with classes and due dates, plus an `.ics` feed students can subscribe to on their phone
- [x] Month view with college dates (holidays, exam and assignment weeks, breaks, closing dates; Admin → Terms), assessment closing dates, one-off extra classes, and personal notes and to-dos with bell reminders; demo data seeds terms, timetables and the college year
- [x] On the live site: publish with the `Phase8Terms` migration, add this year's terms (Admin → Terms) and each module's classes (Lecturer → Timetable), then check dashboards, attendance percentages and a phone subscription

## Phase 9 — Data care and accessibility
- [x] Retention job clears attendance IP and GPS details after the period stated in the POPIA notice (config setting); marks are kept
- [x] Students can download their own data (marks, submissions, attendance) as CSV
- [x] Audit log of admin actions: accounts created or deleted, password resets, enrolment changes
- [x] Accessibility pass to WCAG 2.1 AA: contrast check of the palette, focus handling in dialogs, automated checks of the HTML-checkable axe rules on 48 pages in CI (a browser-based axe run would need a browser and SQL Server in CI); the keyboard-only walk-through is a manual checklist in `docs/ACCESSIBILITY.md`
- [x] README for GitHub: what ISMLTS is and who it's for, features per role with screenshots, the tech stack, running it locally step by step (LocalDB, migrations, demo data and demo accounts), every configuration setting, running the tests, the Android API (link to `docs/API.md`), deploying to Azure, and the team and module details
- [x] Things that were missing: the Android API gained the calendar and calendar notes; `docs/LIVE-TESTING.md` is the step-by-step live test for every phase and role
- [x] Screenshots for the README (`docs/screenshots/*.png`, names listed in README) and the keyboard walk-through on the live site
