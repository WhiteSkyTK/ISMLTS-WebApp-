# ISMLTS — Integrated School Management and Learning Tracking System

WIL Task 1/2 project — Module XADAD7112, Group 1 (ADAD1)
Team: Tokollo Will Nonyane (ST10296818), Phathutshedzo Ramsy Ramakuela (ST10369372), Gundo Mathantshani (ST10367584)
Lecturer: Edward Nkata

## Stack
- **WebApp/** — ASP.NET Core MVC + Web API (C#), Entity Framework Core, hosted on Azure App Service
- **MobileApp/** — Android Studio (Kotlin/Java) native app for students — QR attendance scanning, alerts, tickets
- **Database** — Azure SQL Database

## Setup
- Web: `cd WebApp && dotnet restore && dotnet run`
- Mobile: open `MobileApp/` in Android Studio, let Gradle sync, run on emulator/device

## CI/CD
Two GitHub Actions workflows run on every push/PR to `main`:
- `.github/workflows/dotnet.yml` — restores, builds and tests the web app
- `.github/workflows/android.yml` — builds and unit-tests the Android app

Both currently assume the folder names above (`WebApp/`, `MobileApp/`) — rename the `working-directory` line in each workflow if you scaffold your projects under different folder names.

## Branching
Feature branch → Pull Request into `main` → at least one teammate reviews before merging (required by the module brief).
