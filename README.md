# ISMLTS — Integrated School Management and Learning Tracking System

[![Build, test and deploy](https://github.com/WhiteSkyTK/ISMLTS-WebApp-/actions/workflows/dotnet.yml/badge.svg)](https://github.com/WhiteSkyTK/ISMLTS-WebApp-/actions/workflows/dotnet.yml)

ISMLTS brings a college's day-to-day teaching into one place: modules and enrolments, assessments and file
submissions, marks with feedback, QR-code attendance checked against the campus network and the classroom's location,
tickets, announcements, a timetable and calendar, and early warnings for students at risk. It was built for
**IIE Rosebank College** as the Work Integrated Learning project for module **XADAD7112** (Group 1, ADAD1).

This repository holds the ASP.NET Core MVC website for **admins, lecturers and students**, and the JSON API that the
students' **Android app** uses.

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Tech stack](#tech-stack)
- [Running it on your PC](#running-it-on-your-pc)
- [Configuration](#configuration)
- [Tests and code quality](#tests-and-code-quality)
- [The Android app's API](#the-android-apps-api)
- [Deploying to Azure](#deploying-to-azure)
- [Project structure](#project-structure)
- [Team](#team)

## Features

### Students
- **Dashboard** with current and archived modules, announcements and what's due next.
- **My assessments:** hand in a **PDF, Word or ZIP file** and/or a link, see the late window, keep every earlier upload.
- **Marks and feedback** once the lecturer releases them; **My Progress** with averages, attendance and risk warnings.
- **Scan in** to class with the QR code (checked against the campus Wi-Fi or the classroom's GPS position).
- **Calendar:** classes, due and closing dates, holidays, exam and assignment weeks, plus their own **notes and to-dos
  with reminders**, and a private link to subscribe from their phone's calendar.
- **Portfolio, Awards, Tickets** to their lecturers, **notifications** (bell and optional email) and a weekly digest.
- **Profile:** change password, authenticator two-factor sign-in, and **download all their own data** (POPIA).

### Lecturers
- **Assessments** with due dates and a late window (keep accepting, close at the due date, or 1–7 days late).
- **Markbook, gradebook, Quick Eval** and CSV import; marks have feedback, a change history and a release switch.
- **Submissions:** download students' files (short-lived links), see on-time and late hand-ins.
- **Attendance:** start a QR register in one click for the class on now, mark people present, cancel a class,
  export registers.
- **Timetable** of weekly and extra classes, **Class Insights** with an at-risk list, tickets, announcements, calendar.

### Admins
- Students, lecturers, admins, modules and courses, with search, sorting and paging; **bulk CSV import**.
- Enrol a whole class into a course's modules per term; **terms** and **college dates** (holidays, exams, breaks).
- Password resets, two-factor switch-off, **audit log** of account and enrolment changes, **reports**.
- **Site check:** database and migrations, time zone, the address the site sees, storage, sign-in and email settings.

### Across the site
- Optional **Sign in with Microsoft** for students and lecturers (college Microsoft accounts).
- POPIA **privacy notice**; attendance IP addresses and locations are deleted after 90 days (configurable).
- Accessible (WCAG 2.1 AA checks on every page in CI), works on phones, respects reduced motion.

## Screenshots

Put the images in `docs/screenshots/` with these names and they appear below.

| | |
| --- | --- |
| ![Log-in page](docs/screenshots/login.png) | ![Student dashboard](docs/screenshots/student-dashboard.png) |
| ![Submitting work](docs/screenshots/submit.png) | ![Calendar](docs/screenshots/calendar.png) |
| ![Lecturer markbook](docs/screenshots/markbook.png) | ![Attendance QR code](docs/screenshots/attendance.png) |
| ![Class insights](docs/screenshots/insights.png) | ![Admin site check](docs/screenshots/site-check.png) |

## Tech stack

| Part | What |
| --- | --- |
| Web app | ASP.NET Core MVC on **.NET 9**, C#, Razor views, Bootstrap 5.3 and Bootstrap Icons |
| Data | Entity Framework Core 9 with **SQL Server** (LocalDB on a PC, **Azure SQL** live) |
| Sign-in | Cookie authentication with BCrypt password hashes, TOTP two-factor (Otp.NET), optional Microsoft Entra ID (OpenID Connect) |
| API | JSON under `/api/v1` with JWT access tokens and single-use refresh tokens; Swagger UI in Development |
| Files | **Azure Blob Storage** (private container, SAS download links), or a local folder |
| Hosting | **Azure App Service** (Windows), Azure Communication Services for email, Application Insights |
| Quality | xUnit unit and integration tests, GitHub Actions CI, **SonarCloud** with coverage |

## Running it on your PC

**You need:** Visual Studio 2022 (17.12 or later, with the "ASP.NET and web development" workload, which includes
SQL Server LocalDB) or the .NET 9 SDK plus LocalDB.

1. Clone the repository and open `ISMLTS(WebApp).sln`.
2. Start the **http** or **https** profile (or run `dotnet run --project "ISMLTS(WebApp)" --launch-profile http`).
3. On the first start the app creates the LocalDB database, applies every migration and adds demo data: 3 courses,
   4 lecturers, 30 students, assessments, marks, attendance, tickets, terms, a timetable and the year's college dates.
4. Log in with a demo account. The password for all of them is `12345678`:

   | Role | Log in with |
   | --- | --- |
   | Admin | `admin` (asks you to set up an authenticator app first) |
   | Lecturer | `edward.nkata@rosebank.iie.ac.za`, `nomsa.dlamini@rosebank.iie.ac.za`, `pieter.vanwyk@rosebank.iie.ac.za`, `aisha.patel@rosebank.iie.ac.za` |
   | Student | `st10000001@rcconnect.edu.za` to `st10000030@rcconnect.edu.za` |

5. Other pages: `/swagger` (the API, Development only) and `/health` (database check).
6. **Start again from scratch:** in Package Manager Console run `Drop-Database`, then start the app.

**Changing the database:** in Package Manager Console (default project: the web app) run `Add-Migration <Name>`, then
`Update-Database`. The app also applies new migrations by itself when it starts.

**Uploaded files** go to `ISMLTS(WebApp)/App_Data/submissions` (ignored by git). To try Blob Storage locally, run
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) and set `Storage__ConnectionString`
to `UseDevelopmentStorage=true`.

## Configuration

Settings come from `appsettings.json`, `appsettings.Development.json`, user secrets or environment variables. On
Azure, set them as App Service **App settings** with `__` instead of `:` (for example `Jwt__SigningKey`). Secrets
never go in the repository.

| Setting | Default | What it does |
| --- | --- | --- |
| `ConnectionStrings:ApplicationDbContext` | LocalDB | The SQL Server database (on Azure: a connection string of type SQLAzure) |
| `Seed:AdminPassword`, `Seed:LecturerPassword` | — | Passwords for the first admin (`admin`) and lecturer, used only while there are none |
| `Seed:DemoData`, `Seed:DemoPassword` | on in Development | Adds the demo data once (password of 8+ characters) |
| `Jwt:SigningKey` | random per start | Signs the Android app's tokens; set a 32+ character secret live |
| `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays` | 60, 30 | How long app sign-ins last |
| `TwoFactor:RequiredForAdmins` | `true` | Admins must use an authenticator app |
| `Authentication:Microsoft:ClientId`, `ClientSecret`, `TenantId` | — | Turns on "Sign in with Microsoft" (see below) |
| `RateLimiting:LoginAttemptsPerMinute` | 5 | Log-in attempts per minute per network address |
| `Attendance:SessionMinutes`, `RadiusMeters`, `AllowedIpRanges` | 15, 200, … | How long a QR code works, how close a GPS scan must be, the campus network ranges |
| `Risk:AttendanceThreshold` | 75 | Attendance % below which a student is at risk (as well as an average below 50%) |
| `Students:EmailDomain` | `rcconnect.edu.za` | Student emails must end in this |
| `Privacy:ScanDetailsDays` | 90 | Days before a scan's IP address and location are deleted (shown in the privacy notice) |
| `Storage:ConnectionString` | — | Azure Storage account for uploads; without it files are saved under `Storage:LocalFolder` |
| `Submissions:MaxFileMegabytes` | 20 | Largest upload (at most 25) |
| `Email:ConnectionString`, `Email:From`, `Email:SiteUrl` | — | Azure Communication Services email; nothing is sent without them |
| `ExternalLinks:IieLibrary`, `ExternalLinks:StudentPortal` | — | Shortcut links (hidden while empty) |
| `ForwardedHeaders:Enabled` | `false` | Read the visitor's address from X-Forwarded-For (only if Site check asks for it) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | — | Sends errors and slow requests to Application Insights |
| `WEBSITE_TIME_ZONE` (App Service) | — | Set to `South Africa Standard Time` |

## Tests and code quality

```bash
dotnet test
```

About 640 tests run in a few seconds, with no database to install: services get unit tests, and the security rules
(roles, lecturer module ownership, students' own records, the API's tokens, file downloads, calendar links) get
integration tests against the real app with an in-memory SQLite database. One integration test renders 48 pages for
every role and checks them against the HTML accessibility rules (see `docs/ACCESSIBILITY.md`).

GitHub Actions (`.github/workflows/dotnet.yml`) builds and tests every push and pull request with coverage, and sends
the analysis to SonarCloud once the `SONAR_TOKEN` secret exists (switch off Automatic Analysis in SonarCloud first).

## The Android app's API

Students' Android app talks to `https://<site>/api/v1/`: log in (with two-factor), modules, marks, assessments,
submitting links and files, attendance scans, tickets, notifications, announcements and the calendar.
**[docs/API.md](docs/API.md)** explains tokens, errors and every endpoint, with Kotlin (Retrofit and OkHttp) code and
how to reach a PC's API from the emulator or a phone.

## Deploying to Azure

The live site is an Azure App Service (Windows, .NET 9) with an Azure SQL database. **[docs/LIVE-TESTING.md](docs/LIVE-TESTING.md)**
is the checklist for testing a release on the live site as every role.

### 1. App Service settings (Environment variables → App settings)

At least: the connection string (Connection strings tab, type SQLAzure), `Seed__AdminPassword`,
`Seed__LecturerPassword`, `Jwt__SigningKey`, `WEBSITE_TIME_ZONE` and `Storage__ConnectionString`. Add the optional
ones from the table above as needed. General settings: **HTTPS Only** on, **Always On** on (reminders, the digest
and clean-up jobs run inside the app), and **Health check** path `/health`. On the Azure SQL server, allow Azure
services to connect.

### 2. File storage

Create a **Storage account** (Standard, LRS, the same region, anonymous blob access off). Copy **Access keys →
Connection string** into the App setting `Storage__ConnectionString` (App settings tab, not Connection strings).
The site creates the private `submissions` container on the first upload.

### 3. Sign in with Microsoft (optional)

1. In Microsoft Entra ID (the college's tenant), **App registrations → New registration**: single tenant, redirect URI
   (Web) `https://<site>/signin-microsoft`.
2. **Certificates & secrets → New client secret**; copy its value.
3. **API permissions:** `openid`, `profile`, `email` (Microsoft Graph, delegated). If the college requires it, an IT
   admin grants consent for the organisation.
4. App settings: `Authentication__Microsoft__ClientId` (Application ID), `Authentication__Microsoft__ClientSecret`,
   `Authentication__Microsoft__TenantId` (Directory ID or the college's domain; `common` is refused).

It only signs in an existing student or lecturer whose email is the Microsoft account's sign-in name; it never
creates accounts or signs in admins, and password log-in keeps working.

### 4. Publish

- **Visual Studio:** right-click the web project → Publish to the App Service.
- **GitHub Actions** (every push to `master`): register an app in Entra ID with a federated credential for
  `repo:WhiteSkyTK/ISMLTS-WebApp-:ref:refs/heads/master` and the **Website Contributor** role on the web app; add the
  secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and the variable `AZURE_WEBAPP_NAME`
  (optionally `AZURE_WEBAPP_SLOT` = `staging` on Standard tier). The workflow builds, tests, deploys and waits for
  `/health`. No publish profile or password is stored in GitHub.

### 5. First start and every deploy after it

- The site applies new migrations as it starts, then makes sure the first admin, the first lecturer and the ADAD0701
  course with its 8 modules exist. Existing data is never overwritten.
- With `Seed__DemoData` on, the demo data is added once next to existing data (taken emails are skipped), and terms,
  a timetable and college dates are added while those tables are empty. Switch it off afterwards, and start from a
  fresh database before real students use the site.
- After each deploy: open `/health` (it should say `Healthy`), log in as an admin and open **Site check**. Fix
  anything marked Problem or Check. Add the year's **Terms** and **college dates**, and ask lecturers to fill in their
  **Timetable**.

### 6. Rolling back

- **Code:** re-run the last good GitHub Actions run (builds are kept for 7 days), or publish an earlier commit from
  Visual Studio; with a staging slot, swap back.
- **Data:** migrations only add tables and columns, so earlier code still runs on the newer database. Use Azure SQL's
  point-in-time restore to undo data changes.

## Project structure

```
ISMLTS(WebApp)/        the web app (namespace ISMLTS_WebApp_)
  Controllers/         MVC controllers; Controllers/Api holds the /api/v1 endpoints
  Models/              entities and view models
  Data/                ApplicationDbContext, DataSeeder (required data) and DemoSeeder (demo data)
  Repositories/        one repository interface and class per entity (controllers never touch the DbContext)
  Services/            business rules: marking, risk, attendance checks, files, calendar, terms, accounts, ...
  Views/               Razor views; shared parts in Views/Shared
  wwwroot/             CSS and JavaScript (no inline styles or scripts in views)
ISMLTS.Tests/          xUnit unit tests and Integration/ tests
docs/                  API guide, live-testing checklist, accessibility notes
ROADMAP.md             the phases of work
```

## Team

| Name | Student number |
| --- | --- |
| Tokollo Will Nonyane | ST10296818 |
| Phathutshedzo Ramsy Ramakuela | ST10369372 |
| Gundo Mathantshani | ST10367584 |

Lecturer: Edward Nkata · IIE Rosebank College · WIL module XADAD7112, Group 1 (ADAD1).

Branching: feature branch → pull request into `master` → at least one teammate reviews before merging.
