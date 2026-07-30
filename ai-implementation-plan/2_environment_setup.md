# Plan: Environment Setup (Phase 0)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** Verify the full toolchain for Argus and get MSSQL running locally so the
code-first EF migrations have a target database. This is the prerequisite for every later
phase. Originally written around a Docker container; **rewritten on 2026-07-29 after dropping
Docker** — see Notes.

---

## Checklist

- [x] Confirm .NET SDK 10 installed (`10.0.302`)
- [x] Confirm Node.js installed (`v24.18.0`)
- [x] Confirm pnpm installed (`11.17.0`)
- [x] Confirm Git installed (`2.55.0`)
- [x] Confirm VS Code installed
- [x] Confirm SQL Server LocalDB present (`MSSQLLocalDB`, version 15.0 = SQL Server 2019 engine)
- [x] Point `ConnectionStrings:ArgusDatabase` at `(localdb)\MSSQLLocalDB`
      (`appsettings.Development.json` + `appsettings.Example.json`)
- [x] Fix the design-time fallback in `Database/ArgusDbContextFactory.cs`
- [x] Wire `pnpm run db:up` / `db:down` to `sqllocaldb start|stop MSSQLLocalDB`
- [x] Start the database and confirm it is running (`sqllocaldb info MSSQLLocalDB` → `Running`)
- [x] Verify a client connection to MSSQL
- [x] Apply the first EF migration against the running DB (`InitialCreate`)
- [x] Remove `docker-compose.yml` and the compose-only `MSSQL_SA_PASSWORD` from `.env.example`

---

## Notes

- **Why Docker was dropped.** Docker Desktop's WSL2 VM was consuming over half of the machine's
  RAM to host one database. LocalDB does the same job as an on-demand process at ~150 MB
  (`sqlservr` measured at 147 MB). No container, no VM, no manual "start Docker Desktop" step.
- **Deliberate deviation from `roadplan`: Windows authentication, not SQL authentication.**
  `roadplan` asks for "standard sql server authentication (not trusted connection)". LocalDB
  does not support SQL logins at all — it authenticates as the logged-in Windows user. The
  original requirement made sense for a container with an `sa` account; locally it cannot be
  met. `TrustServerCertificate=True` is kept for the self-signed local certificate.
- **The design-time factory was a hidden trap.** `ArgusDbContextFactory` carried its own
  hardcoded `localhost,1433` fallback, so `dotnet ef` kept timing out against the old Docker
  endpoint even after configuration pointed at LocalDB. Changing configuration alone was not
  enough.
- **Non-Windows machines.** LocalDB is Windows-only. On Linux/macOS the equivalent is a local
  SQL Server install or a container; the connection string is the only thing that changes.
- The demo admin password lives in `Seed:AdminPassword` (`appsettings.Development.json`), which
  is gitignored per `CLAUDE-dotnet.md`. `DbSeeder.SeedUsersAsync` is idempotent — it skips
  entirely once any user exists, so changing that setting has no effect on an existing
  database unless the `ApplicationUsers` row is removed first.
- This plan was reconstructed after the fact. The Docker→LocalDB switch was made without a
  plan file first, contrary to `CLAUDE-planning-standards.MD` — recorded here so the history
  is not silently rewritten.
