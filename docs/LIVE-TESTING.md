# Testing a release on the live site

Work through this after publishing, on the live URL (not localhost), as every role. Tick each line; when something
fails, note the role, the page, what you did and the time, then check **Site check** and the App Service **Log stream**
(or Application Insights).

## 0. Before you start

- [ ] Published from `master`. The site applies new migrations by itself as it starts.
- [ ] `https://<site>/health` says **Healthy**.
- [ ] App settings exist: the connection string, `Seed__AdminPassword`, `Seed__LecturerPassword`, `Jwt__SigningKey`,
      `WEBSITE_TIME_ZONE` = `South Africa Standard Time`, `Storage__ConnectionString` (App settings tab). Optional:
      `Email__*`, `ExternalLinks__*`, `Authentication__Microsoft__*`, `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- [ ] Log in as **admin** and open **Site check**: no **Problem**; "Schema" shows the newest migration; "Submission
      storage" says Blob Storage; "Time zone" is UTC+02:00.
- [ ] Demo data: if you want it, `Seed__DemoData` = `true` and `Seed__DemoPassword` (8+ characters, not `12345678`),
      restart once, then set `Seed__DemoData` = `false`.

Accounts used below: **admin**, a **lecturer** (for example `edward.nkata@rosebank.iie.ac.za`), **student A** enrolled
in one of that lecturer's modules, and **student B** who is not.

## 1. The address the site sees (campus check and log-in limit)

The campus-network check and the log-in rate limit both rely on the site seeing each visitor's real address.

1. On a **phone using mobile data** (Wi-Fi off), search "what is my IP" and note the address.
2. On the same phone, log in as admin and open **Site check** → "Your address".
3. **Same address and OK:** leave `ForwardedHeaders__Enabled` off. Tick the Phase 6 item in ROADMAP.md.
   **Different, marked Check, and X-Forwarded-For shows your phone's address:** set `ForwardedHeaders__Enabled` = `true`,
   wait for the restart, repeat. It should now show the phone's address.
4. From the **campus Wi-Fi**, Site check's "Campus network" line should say your address is in
   `Attendance:AllowedIpRanges`. If not, ask IT for the campus public IP range and set `Attendance__AllowedIpRanges__2`
   (it replaces the example range `203.0.113.0/24`; use `__3`, `__4`, ... for more ranges).

## 2. Signing in and security

- [ ] Wrong password 6 times in a minute → "Too many log-in attempts"; after a minute you can log in again.
- [ ] **Admin:** password, then the authenticator code. A recovery code also works once.
- [ ] **Student:** Profile → set up two-factor, log out, log in with the code; turn it off again.
- [ ] **Admin:** reset student A's password → the temporary password shows once; student A logs in with it and must
      choose a new one before anything else.
- [ ] **Sign in with Microsoft** (only if configured): the button shows on the log-in page; a lecturer or student with a
      matching college account gets in; an unknown account gets "No student or lecturer account … uses …".
- [ ] Log out works from the account menu on every role.
- [ ] Student B opens a lecturer page (for example `/Marks`) → Access denied. Lecturer opens another lecturer's module
      by changing the number in the address → Not found.

## 3. Notifications and announcements (Phase 2)

- [ ] **Lecturer:** post an announcement to a module → student A gets it in the bell and on the dashboard; student B doesn't.
- [ ] **Admin:** post an announcement to everyone → every role sees it.
- [ ] **Student A:** raise a ticket → the lecturer's bell; the lecturer answers → student A's bell (and email, if set up).
- [ ] Bell: unread count, "Mark all read", "View all" with the Unread filter. Notification settings save.

## 4. Assessments and marking (Phases 3 and 7)

- [ ] **Lecturer:** add an assessment with "Allow 2 days late" → enrolled students are notified.
- [ ] **Student A:** submit a **PDF** → toast; then a **.docx** → the Submit page lists both, newest marked "Counts".
- [ ] **Student A:** a `.txt` renamed to `.pdf` is refused ("isn't a real PDF"); nothing is stored.
- [ ] **Lecturer:** Submissions page shows the file and "1 earlier upload kept"; **Download** opens it (the address
      goes to `…blob.core.windows.net` and stops working after 5 minutes).
- [ ] **Student B / another lecturer:** the same download link → Not found.
- [ ] **Lecturer:** Quick Eval → score and feedback → "Save & next"; Gradebook saves a whole column; Markbook shows the grid.
- [ ] Student A doesn't see the mark until the lecturer clicks **Release marks**; then it shows on My Assessments,
      My Marks, My Progress and Portfolio.
- [ ] CSV import of marks shows a preview with row errors; CSV export of marks and of a register open in Excel.
- [ ] An assessment past its window shows **Closed** for students; uploads are refused.

## 5. Attendance (Phases 0 and 8)

- [ ] **Lecturer** (in class or on campus Wi-Fi): Attendance → module → **Start attendance session** → QR code shows.
- [ ] **Student A:** scan the QR with the phone camera (or type the code on Scan In) → "You're marked present".
      Off campus and far away → "We couldn't confirm you're in class".
- [ ] **Lecturer:** register shows the scan; mark someone present by hand; remove a scan; close the session.
- [ ] **Lecturer:** with a timetabled class on now, the Attendance page shows **On now … Take register**.
- [ ] **Lecturer:** mark a session as a cancelled class → it stops counting in students' attendance %.

## 6. Terms, timetable and calendar (Phase 8)

- [ ] **Admin → Terms:** this year's Term 1 and Term 2 exist (seeded, or add them); the current one says **Current**.
      Add a college date (for example an exam week).
- [ ] **Student A:** dashboard **Archived** tab holds the other term's modules.
- [ ] **Lecturer → Timetable:** add a weekly class and an extra class for a date.
- [ ] **Calendar** (every role): month view with classes, due and closing dates, term and college dates; arrows change
      month; on a phone it becomes a list.
- [ ] **Student A:** add a note for a few minutes from now with "Remind me" → within 5 minutes it's in the bell; tick it done.
- [ ] **Student A:** **Get my calendar link** → shown once → add it to Google Calendar (Other calendars → From URL) or the
      phone (Add to my calendar). Classes and due dates appear (Google can take a few hours to refresh).

## 7. Pages behind every link and admin tools (Phases 4 and 9)

- [ ] Profile (change password), Portfolio, Awards, Help (per role), Privacy notice (states the 90-day scan clean-up).
- [ ] IIE Library / Student Portal shortcuts open in a new tab (only if `ExternalLinks__*` are set).
- [ ] **Admin:** bulk import students from CSV (preview, errors, temporary passwords shown once); Reports page.
- [ ] **Admin → Audit log:** the password reset, imports and enrolment changes from above are listed with who and when.
- [ ] **Student A → Profile → Download my data:** a ZIP with profile, marks (released only), submissions, attendance,
      tickets and calendar notes.
- [ ] Keyboard check from `docs/ACCESSIBILITY.md` on at least the log-in page, a delete confirmation and the calendar.

## 8. The Android app (Phase 5)

Point the app at `https://<site>/api/v1/` (release builds use https only). Then, as student A in the app:

- [ ] Log in (and the two-factor code if student A has it on); close and reopen the app → still logged in (refresh token).
- [ ] Modules, marks (released only), assessments, submit a link and a file, attendance, tickets, notifications,
      announcements and the calendar all load.
- [ ] Scan in to an open register from the app.
- [ ] An admin resets student A's password → at the app's next token refresh (within the hour) it sends the student
      back to the log-in screen.

Without the app, the API can be tried with curl (Git Bash). Log in:

```bash
curl -s -X POST "https://<site>/api/v1/auth/login" -H "Content-Type: application/json" -d '{"login":"<student email>","password":"<password>"}'
```

Then use the `accessToken` from the answer:

```bash
curl -s "https://<site>/api/v1/assessments" -H "Authorization: Bearer <accessToken>"
```

## 9. When you're done

- [ ] `Seed__DemoData` is off. Before real students use the site, start from a fresh database (or delete the demo
      courses, students and lecturers).
- [ ] Tick the live items in ROADMAP.md.
