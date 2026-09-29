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
- [ ] Notification entity (UserId, Role, Title, Message, Url, CreatedAt, IsRead) with repository and INotificationService
- [ ] Triggers: assessment posted → enrolled students; mark captured/updated → that student; ticket raised → module lecturer; ticket answered → student; attendance session started → enrolled students ("Attendance is open for XADAD7112 — scan the QR in class", never include the code); lecturer marks a student present or removes a scan → that student; announcement posted → its audience
- [ ] Bell: unread count, latest 8, clicking marks read and follows the link (local URLs only), "Mark all read", "View all" page with All/Unread filter; the "due soon" list stays as a second section
- [ ] Announcement entity: lecturers post to their modules, admins post to everyone; the dashboard Announcements tab uses real data
- [ ] Email for ticket replies (both directions) and a weekly "due this week" digest for students (Azure Communication Services), with an opt-out per user on a Notification settings page
- [ ] Unit tests for NotificationService

## Phase 3 — Marking and progress
- [ ] Marks link to assessments: nullable `Mark.AssessmentId` + `Mark.Feedback`
- [ ] Quick Eval (lecturer): queue of submitted-but-unmarked work across their modules, oldest due first, inline score/max/feedback with "Save & next"
- [ ] Gradebook: per assessment, every enrolled student in one grid, save all at once with per-row validation
- [ ] Students see marks and feedback on My Assessments and My Marks
- [ ] My Progress (student, the "Insights" icon): per-module average, risk badge, attendance %, submitted x of y, next due, with an overall summary on top
- [ ] Class Insights (lecturer): per-module class average, attendance rate, submission rate, and an at-risk list (average below 50% or attendance below `Risk:AttendanceThreshold`, default 75) with quick actions
- [ ] CSV export of module marks and session registers (values escaped properly)
- [ ] Audit log of mark changes (who, when, old → new) shown on the mark
- [ ] Lecturers release marks per assessment; students don't see a mark before it is released
- [ ] CSV import of marks for a whole assessment, with a preview and per-row errors
- [ ] Unit tests for every new calculation

## Phase 4 — Pages behind every link
- [ ] Profile for all roles: details + change password (current password required)
- [ ] Two-factor sign-in with an authenticator app (TOTP via Otp.NET, QR via QRCoder): optional for everyone, required for Admins
- [ ] My Portfolio (student): all submissions with marks and feedback, grouped by module
- [ ] Awards (student): badges computed from existing data (95%+ attendance, top mark in an assessment, every submission on time this term)
- [ ] Help: FAQ per role, ending with "Still stuck? Raise a ticket"
- [ ] Privacy: POPIA notice covering marks, submissions, and the IP and GPS captured at attendance scans (why, who sees it, how long it's kept)
- [ ] IIE Library and Student Portal: URLs from an `ExternalLinks` config section, opening in a new tab; hide an icon when its URL is empty
- [ ] Admin: CSV bulk import of students and lecturers with a preview and per-row errors before saving; reset a user's password; Reports page (users, at-risk by module, attendance by module)

## Phase 5 — API for the Android app
- [ ] `/api/v1` controllers with JWT bearer auth: login, my modules, marks, assessments, submit, tickets, notifications, attendance scan (same AttendanceVerifier)
- [ ] Swagger UI in Development only; share the endpoint list with the Android teammate
- [ ] Integration tests for the API auth rules

## Phase 6 — Go live on Azure
- [x] The App Service site is created and running (published by hand; Phases 0–1 and later changes still to be published)
- [ ] GitHub Actions deploys to an App Service staging slot after the tests pass (OIDC login, no publish profile in the repo); swapping to production stays a manual step
- [ ] `/health` endpoint that checks the database; the App Service health check uses it
- [ ] Confirm on Azure that `RemoteIpAddress` is the student's IP (the login rate limit and the campus-network check both rely on it); add `UseForwardedHeaders` with the App Service proxy if it isn't
- [ ] Application Insights for errors and slow requests, with no personal data in log messages
- [ ] `.gitignore` covers `ISMLTS(WebApp)/bin`, `obj` and the test project's output; the tracked build output is removed from git
- [ ] SonarCloud receives test coverage from CI (coverlet, OpenCover format)
- [ ] Deployment runbook in README: required settings, first-run seeding, rollback

## Phase 7 — File submissions
- [ ] Students can upload a file (PDF, DOCX, ZIP; size limit from config) as well as, or instead of, a link; files go to a private Azure Blob container (Azurite locally), never under wwwroot
- [ ] Lecturers download through short-lived SAS links, after the usual module ownership check
- [ ] File type checked from the file's content, not only its extension; stored names are generated, not taken from the upload
- [ ] Per assessment, the lecturer can close submissions at the due date or allow a late window
- [ ] Earlier uploads are kept as history; the latest one counts
- [ ] Unit tests for the file checks; integration tests that nobody else can download a student's file

## Phase 8 — Terms and timetable
- [ ] Term entity with start and end dates; "current term" drives the dashboards and the Archived filter (replaces the hard-coded `IsCurrentSemester = true`)
- [ ] Timetable slots per module (day, time, venue); "Take register" goes straight to the class happening now
- [ ] Attendance percentages count only the current term's sessions; cancelled classes can be excluded
- [ ] Calendar page for each role with classes and due dates, plus an `.ics` feed students can subscribe to on their phone

## Phase 9 — Data care and accessibility
- [ ] Retention job clears attendance IP and GPS details after the period stated in the POPIA notice (config setting); marks are kept
- [ ] Students can download their own data (marks, submissions, attendance) as CSV
- [ ] Audit log of admin actions: accounts created or deleted, password resets, enrolment changes
- [ ] Accessibility pass to WCAG 2.1 AA: keyboard-only walk-through of every role, contrast check of the palette, focus handling in dialogs, automated axe checks in CI
