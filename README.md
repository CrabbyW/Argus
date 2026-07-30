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

```bash
# 1. Start the database
pnpm run db:up            # sqllocaldb start MSSQLLocalDB

# 2. Install frontend dependencies (once)
pnpm install

# 3. Configure the API
cp apps/netcorebackends/Argus.Api/appsettings.Example.json \
   apps/netcorebackends/Argus.Api/appsettings.Development.json
# then edit it: a Jwt:SigningKey of 32+ characters. The connection string already points at
# LocalDB and uses your Windows account — LocalDB does not support SQL username/password auth.

# 4. Start the API — it applies migrations and seeds demo data on first run
pnpm run dev:api          # http://localhost:5080, Swagger at /swagger

# 5. Start the frontend
pnpm run dev:frontend     # http://localhost:4200
```

Sign in with `msfadmin` and the password in `Seed:AdminPassword`. **Change it before this is
reachable by anyone but you.** Note that `DbSeeder` only creates that user when the table is
empty — changing the setting later does nothing unless the `ApplicationUsers` row is removed.

The UI has three sections, each with its own address:

| Address | What it is |
|---|---|
| `/installations` | The grid. Filters, sorting and paging all live in the query string, so a filtered view can be bookmarked and shared. |
| `/installations/:id/view` | Read-only detail — physical path, repositories, created/modified. |
| `/lookups/:kind` | The five lookup tables. Renaming here updates every installation at once. |
| `/repositories` | Source-control locations, maintained per application. |

`pnpm run dev` starts both apps at once.

---

## The data model

The point of Argus is that nothing shared is written twice. Each shared value lives in its own
lookup table and is referenced by `Id`, so renaming `GAIIS1` is a single-row edit that every
installation picks up at once.

```
Machines ─┐
Applications ─┤
AppStages ─┼──> Installations   (the fact table: 5 FKs + per-installation values)
ProcessorArchitectures ─┤
DnsEndpoints ─┘            (nullable — a worker has no public endpoint)

Applications ──> AppRepositories   (git:// svn:// bitbucket://)
ApplicationUser                    (login)
```

`DnsEndpoints` is a table rather than a column because one DNS name can be a **load balancer**
fronting several machines — the seed data includes exactly that case.

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
  frontend/                Vite + React + Fluent UI
libs/                      reserved for shared code
ai-implementation-plan/    numbered plan files, one per phase
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
happens through the app itself and is out of scope. `Tags` is still free text
(`roadplan` marks it `Tbd: PHASE2`).

## API

| Method | Route | |
|---|---|---|
| POST | `/api/auth/login` | anonymous |
| GET | `/api/auth/me` | |
| GET | `/api/installations` | filter, sort, page |
| GET/POST/PUT/DELETE | `/api/installations/{id}` | delete is soft |
| GET/POST/PUT/DELETE | `/api/lookups/{kind}/{id}` | kind = machines · applications · appstages · processorarchitectures · dnsendpoints |
| GET | `/api/health` | anonymous |
