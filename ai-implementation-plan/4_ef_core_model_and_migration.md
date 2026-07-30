# Plan: EF Core Model and First Migration (Phase 2)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** The core design task of Argus: turn the flat list of installation attributes
from `roadplan` into a **normalized** relational model where every shared value (machine,
application, stage, architecture, DNS endpoint) lives in its own lookup table and is
referenced by `Id`. Renaming `GAIIS1` then touches exactly one row. Built code-first with
EF Core: entities in C#, explicit `IEntityTypeConfiguration<T>` per entity, one migration.

---

## Data model analysis (the requested "necht claude udela analyzu")

`roadplan` lists one installation's attributes flat. Splitting them by *what the value
actually identifies*:

| Roadplan attribute | Goes to | Why |
|---|---|---|
| `MachineName` | `Machines` lookup | One server hosts many installations |
| `AppName` | `Applications` lookup | One app is installed in many places |
| `AppStageName` | `AppStages` lookup | Closed set: Staging/RC0/Main/PenTest/Mirror |
| `ProcessorArchitecture` | `ProcessorArchitectures` lookup | Closed set: x86/x64/arm/arm64 |
| `DnsName` | `DnsEndpoints` lookup | **The key case**: a DNS name can be a load balancer pointing at several machines, so it cannot live on the installation row |
| `AppRepositories` | `AppRepositories` table, FK → `Applications` | A repository is where the *application's source* lives, not a property of one deployment; 1:N from the app |
| `RootPath`, `PhysicalPath`, `IsActive`, `ValidFromDate`, `ValidToDate`, `IsEnabled`, `Tags` | `Installations` columns | Genuinely per-installation values, not shared |
| login credentials | `ApplicationUser` | Separate concern (auth) |

Result: **8 tables** — 5 lookups + `Installations` (the fact table) + `AppRepositories` +
`ApplicationUser`.

---

## Checklist

- [x] `Machines` entity + configuration (unique `MachineName`)
- [x] `Applications` entity + configuration (unique `AppName`)
- [x] `AppStages` entity + configuration (unique `StageName`, `SortOrder`)
- [x] `ProcessorArchitectures` entity + configuration (unique `ArchitectureName`)
- [x] `DnsEndpoints` entity + configuration (unique `DnsName`)
- [x] `AppRepositories` entity + configuration (FK → `Applications`, `RepositoryType` enum)
- [x] `Installations` entity + configuration (5 FKs, unique deployment key, indexes)
- [x] `ApplicationUser` entity + configuration (unique `Username`, hash + salt)
- [x] `RepositoryType` enum in `Database/Entities/Enums/`
- [x] `ArgusDbContext` applying configurations from assembly
- [x] Seed data (`DbSeeder`) — small demo set only, no real/bulk data
- [x] Generate the first migration (`InitialCreate`)
- [x] Apply migration to a live database (`dotnet ef database update`) _(applied to SQL Server LocalDB on 2026-07-29 — see `2_environment_setup.md`)_

---

## Notes

- **`Tags` stays a plain string** for now, exactly as `roadplan` marks it (`Tbd: PHASE2`) and
  as flagged in `1_argus_demo_build.md`. Promoting it to `Tags` + `InstallationTags` tables is
  a later change; it is isolated to one column, so the migration cost is low.
- **`DnsEndpointId` is nullable** (assumption confirmed as reasonable): a background service
  or console app installation legitimately has no DNS name. All other FKs are required.
- **Soft delete:** `IsEnabled` is the soft-delete flag (`0` hidden, `1` active) and is applied
  as a **global query filter** on every entity, so ordinary queries never see deleted rows.
  `IsActive` on `Installations` is a *different, business* flag ("is this deployment currently
  serving") and is deliberately kept separate — `roadplan` lists both.
- **Unique deployment key** on `Installations`:
  (`MachineId`, `ApplicationId`, `AppStageId`, `RootPath`) — the same app+stage cannot be
  installed twice at the same path on the same machine.
- Dates `ValidFromDate` / `ValidToDate` are `DateOnly` (mapped to SQL `date`), matching the
  `YYYYMMDD` form in `roadplan`; `ValidToDate` is nullable = "still valid".
- Passwords are stored as PBKDF2-SHA256 hash + per-user salt, never plaintext.
