# Dentiva

**Dentiva** is a premium, local-first **Dental Practice Management System** for Windows —
patient records, appointments and serial (queue) management, clinical visits with a
tooth chart, billing with invoices and money receipts, finance, staff, reports,
attachments, backup/restore and full English/Bengali localization.

Dentiva works fully offline. No cloud service, subscription or API key is required.

- **Developer:** Md. Shohan Khan
- **Contact:** helloiamshohan@gmail.com · WhatsApp 01516591935

> Full documentation (architecture, database, testing, release) lives in [`docs/`](docs).

## Feature overview

| Area | Highlights |
| --- | --- |
| Clinical | Patients, unlimited visit history, tooth chart, treatment plans, prescriptions, referrals, attachments |
| Front desk | Appointments with automatic serial numbers, today's queue, walk-ins, rescheduling |
| Billing | Invoices with line items, discounts and tax, payments with configurable methods, money receipts |
| Business | Income/expense tracking, financial dashboard with period selection, staff and salary tracking |
| Insight | Patient, clinical, appointment, financial and staff reports; global FTS search |
| Platform | SQLite (WAL) storage, backup/restore, CSV/Excel/JSON export, print + real PDF output, audit trail, login with roles |

## Technology

- C# / .NET 8 (LTS), WPF, MVVM, light-theme-only design system
- SQLite via Microsoft.Data.Sqlite + Dapper (WAL mode, enforced foreign keys, migrations, FTS5 search)
- Dependency injection (Microsoft.Extensions.DependencyInjection)
- Embedded Inter + Noto Sans Bengali fonts (SIL OFL) — full Bengali localization
- Zero paid services; printing uses the native Windows print pipeline; PDF is generated in-app

## Building from source

Requirements: **Windows 10 1809+ / Windows 11** and the **.NET 8 SDK**.

```powershell
git clone https://github.com/heyiamshohan-cloud/Dentiva-Official.git
cd Dentiva-Official
dotnet build Dentiva.sln -c Release
dotnet run --project src/Dentiva.App -c Release
```

Produce a deployable build:

```powershell
dotnet publish src/Dentiva.App/Dentiva.App.csproj -c Release -r win-x64 --self-contained false -o publish/app
```

`publish/app/Dentiva.exe` is the application.

## Tests

```powershell
dotnet test Dentiva.sln -c Release
```

The application also ships built-in verification modes:

- `Dentiva.exe --selftest [--data-dir <path>]` — boots the real service stack against an
  isolated data directory and validates core flows end-to-end.
- `Dentiva.exe --qa-tour --out <dir>` — renders every principal screen and writes
  screenshots (used by CI for visual QA).
- `Dentiva.exe --bench --data-dir <path>` — seeds a large synthetic dataset and reports
  query timings (development only; never written to a production database).

## Data, backup and printers

- Database default location: `%LOCALAPPDATA%\Dentiva\data` (configurable in Settings → Data).
- Backup/restore is available in Settings → Backup; backups are plain files that can be
  copied to any drive.
- Any Windows-installed printer works, including A4, letter and thermal/receipt printers;
  paper size, margins and templates are configurable.

## Third-party software

Dentiva depends on and thanks: .NET (MIT), WPF (MIT), Microsoft.Data.Sqlite & Dapper
(Apache-2.0/MIT), Inter and Noto Sans Bengali fonts (SIL OFL 1.1).
See [docs/THIRD-PARTY-NOTICES.md](docs/THIRD-PARTY-NOTICES.md).

## License

Copyright © Md. Shohan Khan. All rights reserved. Licensing for commercial distribution
is defined in the accompanying release documentation.
