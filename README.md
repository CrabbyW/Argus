# Argus

Installation inventory — **where is what installed**. Which application, at which stage, on
which machine, behind which DNS name, in which folder.

NX monorepo (pnpm) · .NET 10 + EF Core (code-first) + MSSQL · React 19 + TypeScript + Fluent UI (Vite) · JWT auth.

---

## Objective

**Argus is the single authoritative answer to "where is this application actually installed?"**

Today that answer lives in people's heads, in spreadsheets, and in IIS consoles on individual
servers. It goes stale the moment a machine is renamed, a stage is promoted, or a DNS name is
re-pointed — and nobody notices until a deployment lands somewhere it shouldn't. Argus replaces
that with one queryable inventory: for every installation, which **application**, at which
**stage**, on which **machine**, under which **DNS name** and **path**, on which
**architecture**, valid **between which dates**.

It is a read-often, write-rarely system of record for the people who deploy and operate these
applications — not a deployment tool. Argus records reality; it does not change it.

**Success looks like:**

- Any deploy or ops question — *which machines serve `paha.ga.local`? where does RC0 of
  ProAssistNet run? what is still installed on GAIIS2?* — is answered from Argus in seconds.
- Shared facts are stored exactly once. Renaming a machine or re-pointing a DNS endpoint is a
  single-row edit that every affected installation picks up immediately.
- History survives. Decommissioning is a soft delete plus a `ValidToDate`, so "what was here
  last quarter?" is still answerable.
- The inventory stays current because keeping it current is cheaper than the workarounds —
  one screen, search and filter, no ceremony.

**Deliberately not in scope:** provisioning, deploying, or configuring anything; monitoring
uptime or health of the installed apps; auto-discovery of installations. Argus is edited by
humans through its own UI and API.

---

## Running it

You need: .NET SDK 10, Node + pnpm. The database is SQL Server **LocalDB**, which ships with the
SQL Server Express / SSDT installers and runs as an on-demand local process (~150 MB) — no Docker,
no container, no WSL2 VM.

```powershell
# 1. Start the database
pnpm run db:up            # sqllocaldb start MSSQLLocalDB

# 2. Install frontend dependencies (once)
pnpm install

# 3. Configure the API
Copy-Item apps/netcorebackends/Argus.Api/appsettings.Example.json `
          apps/netcorebackends/Argus.Api/appsettings.Development.json
# then edit it: a Jwt:SigningKey of 32+ characters, and a Seed:AdminPassword. The connection
# string already points at LocalDB and uses your Windows account — LocalDB does not support
# SQL username/password auth.

# 4. Start the API — it applies migrations and seeds demo data on first run
pnpm run dev:api          # http://localhost:5080, Swagger at /swagger

# 5. Start the frontend
pnpm run dev:frontend     # http://localhost:4200
```

Sign in as **`msfadmin`** (the username `DbSeeder` creates) with whatever you put in
`Seed:AdminPassword`. Two things about that password:

- **`DbSeeder` only seeds a user when the table is empty.** Once the database exists, editing
  `Seed:AdminPassword` does nothing — the seeder skips the whole step. To change the password you
  have to delete the row from `ApplicationUsers` and restart the API.
- **It is a demo credential.** Change it before this is reachable by anyone but you, and see the
  pre-deployment checklist below.

**Not on Windows?** LocalDB is Windows-only. Everything else runs anywhere, so point
`ConnectionStrings:ArgusDatabase` at any SQL Server you can reach — a local install or a
`mcr.microsoft.com/mssql/server` container on port 1433 — using SQL authentication:
`Server=localhost,1433;Database=Argus;User Id=sa;Password=...;TrustServerCertificate=True;`.
Nothing else changes; the migrations and the code are provider-identical.

The UI has three sections, each with its own address:

| Address | What it is |
|---|---|
| `/installations` | The grid. Filters, sorting and paging all live in the query string, so a filtered view can be bookmarked and shared. |
| `/installations/:id/view` | Read-only detail — physical path, repositories, created/modified. |
| `/lookups/:kind` | The five lookup tables. Renaming here updates every installation at once. |
| `/repositories` | Source-control locations, maintained per application. |

`pnpm run dev` starts both apps at once.

---

## Tests, lint, CI

```bash
pnpm run test     # xUnit suite for the API (SQLite in memory — no SQL Server needed)
pnpm run lint     # eslint over the frontend
pnpm run build    # dotnet build + tsc -b + vite build
```

The test suite runs the real EF model against SQLite in memory rather than the InMemory provider,
so unique indexes, filtered indexes and `EF.Functions.Like` behave as they do in production. It
covers the two defects that have actually bitten this project — reinstalling a decommissioned
deployment, and a lookup edit silently clearing a field it never read — plus the installation
filter, auth and password hashing.

`.github/workflows/ci.yml` runs lint, build and test on every push and pull request. It needs no
database, so it runs on `ubuntu-latest`.

---

## The data model

The point of Argus is that nothing shared is written twice. Each shared value lives in its own
lookup table and is referenced by `Id`, so renaming `GAIIS1` is a single-row edit that every
installation picks up at once.

```
Machines ──────────────┐
AppNames ──────────────┤
AppStageNames ─────────┤
ProcessorArchitectures ─┼──> ApplicationInstallations
DnsEndpoints ──────────┤     (the fact table: 7 FKs + its own dates and flags)
RootPaths ─────────────┤
PhysicalPaths ─────────┘     DnsEndpointId and PhysicalPathId are nullable —
                             a background worker has neither

Tags ──< InstallationTags >────────┐
AppRepositories ──< InstallationRepositories >──┴──> ApplicationInstallations

ApplicationUser                    (login)
```

Thirteen tables: eight plain lookups, `AppRepositories`, the installation itself, two link
tables and the user.

The installation row holds **no names of its own** — every shared value is an `Id` into a lookup
that must already exist. Its own columns are `IsActive`, `ValidFromDate`, `ValidToDate`,
`IsEnabled`, `CreatedUtc`, `ModifiedUtc`: values that genuinely belong to one deployment and are
not shared with any other.

`DnsEndpoints` is a table rather than a column because one DNS name can be a **load balancer**
fronting several machines — the seed data includes exactly that case.

`Tags` and `AppRepositories` are many-to-many. A plain foreign key for repositories would have
stored the same url once per installation, which is the duplication the whole model exists to
prevent. The link tables carry no `IsEnabled`: soft delete lives on the ends of a relationship,
never on the relationship itself.

Full analysis: [`ai-implementation-plan/4_ef_core_model_and_migration.md`](ai-implementation-plan/4_ef_core_model_and_migration.md).

---

## Layout

```
apps/
  netcorebackends/         .NET solution
    Argus.Api/
      Controllers/         Installations, Lookups, Auth, Health
      Services/            business logic (interface + implementation)
      Mappers/             static entity -> DTO
      WebApiPoco/          DTOs (Common/, Installations/, Auth/)
      Database/
        Entities/          EF entities
          Configurations/  IEntityTypeConfiguration<T> per entity
          Enums/
        Migrations/        InitialCreate
      Middleware/          GlobalExceptionHandlerMiddleware
    Argus.Api.Tests/       xUnit, SQLite in memory
  frontend/                Vite + React + Fluent UI
libs/                      reserved for shared code
ai-implementation-plan/    numbered plan files, one per phase
.github/workflows/         CI: lint, build, test
secrets/                   gitignored
```

---

## Conventions

Backend follows [`CLAUDE-dotnet.md`](CLAUDE-dotnet.md): log4net with a lowercase static
`logger` field (never `Console.Write`), `ApiResponse<T>` / `ErrorResponse` wrappers,
`[EndpointName]` + `[ProducesResponseType]` on every action, explicit entity configurations,
`AsNoTracking()` on reads, `appsettings*.json` never committed.

Planning follows [`CLAUDE-planning-standards.MD`](CLAUDE-planning-standards.MD): every phase
has a numbered plan file in `ai-implementation-plan/` with a live checklist and status.

Where the template's conventions did not transfer (Windows/Negotiate auth, DatexPush endpoints,
shared RSA keys between two backends), the deviation is recorded in the relevant plan file
rather than silently dropped.

---

## Scope

This is a **deploy-ready demo**: feature-complete skeleton, seed data only. Loading real data
happens through the app itself and is out of scope.

All nine shared values from `roadplan` are now their own tables, filled before the installation
row that references them. `Tags` became `Tags` + `InstallationTags` on 2026-07-30 — it is no
longer free text — and `AppRepositories` moved from the application onto the installation as a
many-to-many link. See `ai-implementation-plan/10_schema_normalization.md`.

### Known deviation from `roadplan`

**Database authentication.** The roadplan asks for SQL username/password authentication. That
requirement assumed the database ran in a container; after Docker was dropped on 2026-07-30 the
database is LocalDB, and **LocalDB has no SQL authentication at all** — it only accepts the Windows
account that owns the instance. The connection string therefore uses `Trusted_Connection=True`.
This is the one place the implementation knowingly differs from the roadplan, it is recorded here
and nowhere else, and it disappears by itself on any real SQL Server: point
`ConnectionStrings:ArgusDatabase` at one and use SQL auth as described under *Running it*.

The full flow has been exercised in a browser end to end — login (including a rejected password),
the grid with search, facets, sorting and paging, create → edit → soft delete, and the lookup
screens including a machine rename propagating to every installation that references it.

Re-verified on 2026-07-31 against the normalized schema: build, 68 tests, `tsc` and lint all clean,
the database rebuilt from `InitialCreate` to the exact seed counts, and the `10_schema_normalization.md`
§6 walkthrough run end to end. It found one bug — deep-linked filters were dropped on load — which is
fixed and recorded in that file's §9.

---

## Before anyone else can reach this

Everything below is deliberately configured for a local demo. None of it is safe on a shared
network, and none of it is difficult to change:

- **Rotate the admin password.** `msfadmin` with a demo password is a demo credential. Because the
  seeder skips a non-empty table, this means deleting the `ApplicationUsers` row and restarting
  with a new `Seed:AdminPassword` — or adding a password-change endpoint, which does not exist yet.
- **Move `Jwt:SigningKey` out of `appsettings`.** It is a symmetric HMAC key: whoever holds it can
  mint tokens for any user. Environment variable, user-secrets or a secret store — the file is
  gitignored, which is not the same as protected. Startup already refuses to boot without 32+
  characters.
- **Turn off `Database:MigrateAndSeedOnStartup`.** Convenient locally, wrong anywhere schema
  changes should be applied deliberately; run `pnpm run db:migrate` as a deployment step instead.
  Leaving it on also re-seeds demo lookup rows into an empty production database.
- **Set `Cors:AllowedOrigins` and `AllowedHosts`.** They are `http://localhost:4200` and `*`.
- **Serve over HTTPS.** The token sits in `localStorage` and travels on every request.
- **Decide on packaging.** There are no container images and no deployment pipeline — see
  `ai-implementation-plan/8_deploy_packaging.md`, where this is deliberately left open.

---

## Where the rest of the documentation lives

This README is the entry point; everything else is history or reference.

| File | What it is |
|---|---|
| `roadplan` | The original brief — what the app must do. Still current. |
| `docs/reference/zdrojova-tabulka-*.png` | Screenshots of the spreadsheet the brief came with, one per sheet. The source the data model was derived from. |
| `ai-implementation-plan/1..10_*.md` | One plan per phase, each with its own checklist and notes on deviations. Reference. |
| `progress.txt` | Dated build log in Czech: what was done when, and why. Historical. |
| `PREHLED-projektu-CZ.txt` | Czech orientation and glossary, written before any code existed. Historical — its "there is no code yet" framing no longer applies. |
| `CLAUDE-dotnet.md`, `CLAUDE-planning-standards.MD` | Conventions inherited from another project; see Conventions above. |

## API

| Method | Route | |
|---|---|---|
| POST | `/api/auth/login` | anonymous |
| GET | `/api/auth/me` | |
| GET | `/api/installations` | filter, sort, page |
| GET/POST/PUT/DELETE | `/api/installations/{id}` | delete is soft |
| GET/POST/PUT/DELETE | `/api/lookups/{kind}/{id}` | kind = machines · applications · appstages · processorarchitectures · dnsendpoints |
| GET | `/api/health` | anonymous |
