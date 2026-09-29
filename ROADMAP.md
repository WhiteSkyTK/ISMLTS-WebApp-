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
- [ ] Shared partials `_PageHeader` (title, subtitle, back link, primary action) and `_EmptyState`, used on every page
- [ ] Toasts: the layout renders `TempData["Toast"]` as an auto-hiding Bootstrap toast; every create/edit/delete/submit sets one
- [ ] `data-loading` on submit buttons (disable + spinner, no double posts); `data-confirm` deletes through one shared modal; retire the separate Delete pages
- [ ] Row actions become small icon buttons with tooltips; tables get a search box (`data-table-filter`), sticky headers and horizontal scroll on mobile; admin lists paginate past 25 rows
- [ ] Nav highlights the active link; admin nav gains Courses and Admins; the icon row becomes role-specific (pointing at Phase 3/4 pages); remove Language from the avatar menu
- [ ] Styled 404, 403 and error pages via `UseStatusCodePagesWithReExecute`
- [ ] Subtle motion (page fade-in, card hover lift, unread-badge pulse), all off under `prefers-reduced-motion`
- [ ] Remove every remaining inline `style=""` from views

## Phase 2 — Notifications and announcements
- [ ] Notification entity (UserId, Role, Title, Message, Url, CreatedAt, IsRead) with repository and INotificationService
- [ ] Triggers: assessment posted → enrolled students; mark captured/updated → that student; ticket raised → module lecturer; ticket answered → student; attendance session started → enrolled students ("Attendance is open for XADAD7112 — scan the QR in class", never include the code); lecturer marks a student present or removes a scan → that student; announcement posted → its audience
- [ ] Bell: unread count, latest 8, clicking marks read and follows the link (local URLs only), "Mark all read", "View all" page with All/Unread filter; the "due soon" list stays as a second section
- [ ] Announcement entity: lecturers post to their modules, admins post to everyone; the dashboard Announcements tab uses real data
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