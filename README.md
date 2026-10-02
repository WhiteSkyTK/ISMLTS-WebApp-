# ISMLTS — Integrated School Management and Learning Tracking System

WIL Task 1/2 project — Module XADAD7112, Group 1 (ADAD1)
Team: Tokollo Will Nonyane (ST10296818), Phathutshedzo Ramsy Ramakuela (ST10369372), Gundo Mathantshani (ST10367584)
Lecturer: Edward Nkata

## What is in this repository

- **`ISMLTS(WebApp)/`** — the ASP.NET Core MVC web app (.NET 9, C#, Entity Framework Core, SQL Server) for admins,
  lecturers and students, plus the JSON API under `/api/v1` for the student Android app
- **`ISMLTS.Tests/`** — xUnit unit and integration tests
- **`docs/API.md`** — how the Android app talks to the API
- **`ROADMAP.md`** — the phases of work, done and to come

The Android app is built separately and calls the API described in `docs/API.md`.

## Running it on your PC

1. Open `ISMLTS(WebApp).sln` in Visual Studio 2022 (or run `dotnet run --project "ISMLTS(WebApp)" --launch-profile http`).
2. Start the `http` or `https` profile. On first start the app creates the LocalDB database, applies every migration
   and, in Development, adds demo data: 3 courses, 4 lecturers, 30 students, marks, attendance and tickets.
3. Log in with any demo account; the password is `12345678`:

   | Role | Log in with |
   | --- | --- |
   | Admin | `admin` (sets up an authenticator app at first log-in) |
   | Lecturer | `edward.nkata@rosebank.iie.ac.za`, `nomsa.dlamini@rosebank.iie.ac.za`, `pieter.vanwyk@rosebank.iie.ac.za`, `aisha.patel@rosebank.iie.ac.za` |
   | Student | `st10000001@rcconnect.edu.za` to `st10000030@rcconnect.edu.za` |

4. Start again from scratch: in Package Manager Console run `Drop-Database`, then start the app.

Other things you can open: `/swagger` (the API, Development only) and `/health` (database check).

Uploaded files are saved in `ISMLTS(WebApp)/App_Data/submissions` on your PC (ignored by git). To try Azure Blob
Storage locally instead, run [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) and set
`Storage__ConnectionString` to `UseDevelopmentStorage=true`.

Run the tests with `dotnet test` from the repository folder.

## Deploying to Azure

The live site is an Azure App Service (Windows, .NET 9) with an Azure SQL database.

### 1. App Service settings (Configuration → Application settings)

| Setting | Value | Needed |
| --- | --- | --- |
| Connection string `ApplicationDbContext` (type SQLAzure) | The Azure SQL connection string | Yes |
| `Seed__AdminPassword`, `Seed__LecturerPassword` | Passwords for the first admin (`admin`) and lecturer, used only when the database has none | First run |
| `Jwt__SigningKey` | A random secret of 32+ characters; signs the Android app's tokens | Yes |
| `WEBSITE_TIME_ZONE` | `South Africa Standard Time`, so due dates and times match the college | Yes |
| `ForwardedHeaders__Enabled` | `true` only if the Site check page says the site sees the proxy's address | If needed |
| `Email__ConnectionString`, `Email__From`, `Email__SiteUrl` | Azure Communication Services, a verified sender, the site's https address | Optional |
| `ExternalLinks__IieLibrary`, `ExternalLinks__StudentPortal` | https addresses for the shortcut row | Optional |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | From an Application Insights resource (paste it here rather than using the portal's codeless switch) | Optional |
| `Seed__DemoData`, `Seed__DemoPassword` | `true` and a password of 8+ characters (not `12345678`) to add the demo data once, for testing | Optional |
| `Storage__ConnectionString` | An Azure Storage account's connection string (Access keys → Connection string). Uploaded submissions go to its private `submissions` container; without it they are saved on the web server's disk | Yes, for uploads |
| `Submissions__MaxFileMegabytes` | Largest upload in MB (default 20, at most 25) | Optional |

General settings: **HTTPS Only** on, **Always On** on (Basic tier or higher; the Monday digest needs it), and
**Health check** path `/health`. On the Azure SQL server, allow Azure services to connect.

For uploaded submissions, create a **Storage account** (Standard, LRS, same region) and leave anonymous blob access
off. Copy its connection string into `Storage__ConnectionString`; the site creates the private `submissions`
container on the first upload and hands files out through download links that expire after 5 minutes.

### 2. Publish

- **From Visual Studio:** right-click the web project, then Publish to the App Service.
- **From GitHub Actions** (`.github/workflows/dotnet.yml`, on every push to `master`):
  1. In Microsoft Entra ID, register an app, add a federated credential for GitHub Actions with the subject
     `repo:WhiteSkyTK/ISMLTS-WebApp-:ref:refs/heads/master`, and give it the **Website Contributor** role on the web app.
  2. In the GitHub repository (Settings → Secrets and variables → Actions) add the secrets `AZURE_CLIENT_ID`,
     `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID`, and the variable `AZURE_WEBAPP_NAME`.
  3. Optional: on Standard tier or higher, create a `staging` slot and set the variable `AZURE_WEBAPP_SLOT` to
     `staging`; pushes then go to the slot and you swap it to production in the portal.

  The workflow builds, tests, deploys and then waits for `/health` to answer. No publish profile or password is
  stored in GitHub.

### 3. First start and every deploy after it

- The site applies new database migrations by itself as it starts, then makes sure the first admin, the first
  lecturer and the ADAD0701 course with its 8 modules exist. Existing data is never overwritten.
- With `Seed__DemoData` on, the demo courses, students, marks and attendance are added once, next to existing data.
  Accounts whose email already exists are skipped. Switch the setting off afterwards. Before real students use
  the site, start from a fresh database instead of keeping the demo data.
- The first admin log-in asks for an authenticator app (admins must use two-factor sign-in).
- Add the year's terms under **Terms** (admin) so dashboards and attendance follow the term, and ask lecturers to fill in their **Timetable**.
- After each deploy: open `/health` (it should say `Healthy`), log in as an admin and open **Site check**. Fix
  anything marked Problem or Check. It shows unapplied migrations, the time zone, the address the site sees, and
  which settings are missing.

### 4. Rolling back

- **Code:** in GitHub Actions open the last good run of the workflow and choose **Re-run jobs**; it deploys that
  build again (builds are kept for 7 days). From Visual Studio, check out the earlier commit and publish it. With a
  staging slot, swap the slots back.
- **Data:** migrations only add tables and columns, so the earlier code still runs against the newer database.
  To undo data changes, use Azure SQL's point-in-time restore.

### 5. The Android app on the live site

Point the app at `https://<your-site>/api/v1/`; make sure `Jwt__SigningKey` is set first. See `docs/API.md`.

## CI

`.github/workflows/dotnet.yml` builds and tests every push and pull request with coverage. Once a `SONAR_TOKEN`
secret exists it also sends the analysis and coverage to SonarCloud (switch off Automatic Analysis in SonarCloud
first, under Administration → Analysis Method). The deploy job is described above.

## Branching

Feature branch → Pull Request into `master` → at least one teammate reviews before merging (required by the module brief).
