# Plan: Argus – Demo Build (deploy-ready MVP skeleton)

**Date:** 2026-07-28
**Status:** `Complete` — 2026-08-01. Every phase is checked off and the stack has been verified
against a live LocalDB and in a browser; see Phase 6. The one item left unchecked is container
images, which `8_deploy_packaging.md` records as deliberately out of scope.
**Description:** Build project **Argus**, a deploy-ready demo of an installation-inventory
web app ("where is what installed"). The goal is a **functionally complete, deployable
skeleton** — every feature works end-to-end (UI → API → EF Core → MSSQL and back), the
database schema is generated from C# code-first migrations, and the app ships with only a
small **seed** of example rows. Real data is loaded later, through the app itself, and is
out of scope here. The core design task is the **normalized data model** (each shared thing
— machine, application, stage, architecture, DNS — lives in its own lookup table and is
referenced by Id, so a name is edited in exactly one place). Stack: NX monorepo + pnpm,
MSSQL (SQL auth), C#/.NET Core 10 + EF Core (code-first), React + TypeScript + Fluent UI +
Vite, JWT auth via `ApplicationUser`. Backend follows all conventions in `CLAUDE-dotnet.md`;
every phase below gets its own numbered sub-plan per `CLAUDE-planning-standards.MD`.

---

## Definition of Done (demo ready to deploy)

- App builds and runs via a single documented path (SQL Server LocalDB + `pnpm dev`; no Docker).
- DB schema created from EF migrations (`dotnet ef database update`).
- All MVP features work UI ↔ API ↔ DB; endpoints protected by JWT login.
- Config (connection string, JWT keys) kept out of source (`appsettings*` gitignored, `secrets/`).
- Small seed data present so the demo is not empty. **No real/bulk data.**

---

## Checklist

### Phase 0 — Environment
- [x] Confirm toolchain: .NET SDK 10, Node + pnpm, VS Code (+ C#, SQL, ESLint)
- [x] Run MSSQL and verify a client connection _(SQL Server LocalDB, `sqllocaldb start MSSQLLocalDB`; Windows auth — LocalDB does not support SQL logins, see `2_environment_setup.md`)_
- [x] Create sub-plan `2_environment_setup.md`

### Phase 1 — Monorepo skeleton
- [x] Initialize NX monorepo with pnpm (`apps/`, `libs/`, `nx.json`)
- [x] Placeholder backend app + frontend app registered in NX
- [x] Create sub-plan `3_nx_monorepo_skeleton.md`

### Phase 2 — Backend data layer (the core design)
- [x] Define EF Core entities from the normalized model _(the five lookups planned here grew to
      ten on 2026-07-30/31 and the entities were renamed — `Machines`, `AppNames`, `AppStageNames`,
      `ProcessorArchitectures`, `DnsEndpoints`, `RootPaths`, `PhysicalPaths`, `Tags`,
      `AppRepositories`, `RepositoryTypes`, plus `ApplicationInstallations`, two link tables and
      `ApplicationUser`; see plan 10)_
- [x] Add `IEntityTypeConfiguration<T>` per entity; DbContext wiring
- [x] Create the FIRST migration; add small seed data _(the original two migrations were collapsed
      into `InitialCreate` during the 2026-07-30 normalization and applied to a live LocalDB on
      2026-07-31)_
- [x] Create sub-plan `4_ef_core_model_and_migration.md`

### Phase 3 — REST API (CRUD)
- [x] Controllers for Installations + lookups per `CLAUDE-dotnet.md`
      (ApiResponse<T>/ErrorResponse wrappers, `[EndpointName]`, `[ProducesResponseType]`,
      lowercase log4net `logger`, GlobalExceptionHandlerMiddleware)
- [x] Service + static Mapper + Filter DTO (`DataViewFilterBase<T>`) patterns
- [x] Create sub-plan `5_rest_api_crud.md`

### Phase 4 — Frontend
- [x] React + TS (Vite) app with Fluent UI: Installations list (search, paging) + detail/edit
- [x] Lookups shown as dropdowns (Id-backed), not free text
- [x] Wire frontend to the API _(hand-written typed client; `[EndpointName]` attributes are in place so generation can be added later)_
- [x] Create sub-plan `6_frontend_installations_ui.md`

### Phase 5 — Authentication
- [x] `ApplicationUser` login (username + password hashed), JWT issuance
- [x] Protect API endpoints; login screen on frontend
- [x] Create sub-plan `7_authentication_jwt.md`

### Phase 6 — Deployability & polish
- [x] Lookup management screens (Machines/Apps/Stages/Architectures/Dns)
- [x] Filtering/sorting/pagination end-to-end
- [x] LocalDB run path + config externalized; README run steps
- [x] Create sub-plan `8_deploy_packaging.md`
- [x] Verify the running stack end-to-end _(unblocked once the Windows machine had the .NET SDK:
      API exercised 2026-07-29, browser click-through 2026-07-30 (`9_frontend_ux_overhaul_and_fixes.md`),
      re-verified against the normalized schema 2026-07-31 (`10_schema_normalization.md` §9) and
      against the Id/name grid 2026-08-01 (`11_main_grid_as_the_source_sheet.md` §5))_

---

## Notes

- **Scope (confirmed with user):** deploy-ready DEMO — feature-complete skeleton, empty of
  real data (only seed). Bulk/real data import is a later phase, explicitly out of scope.
- **Roadplan example values** (GAIIS1, ProAssistNet, Ids 27/28…) are illustrative only.
- **Normalization rationale:** Id is a stable pointer; a name lives in one row, so renaming
  is a single-place edit and stays consistent everywhere (single source of truth).
- **`CLAUDE-dotnet.md` is a template** from another project (TrafficEvent/DATEX II). Its
  **conventions are binding**, but example entities are replaced with Argus ones
  (Installation etc.). To adapt: swap `TrafficEvent*` → `Installation*`.
- **Planning-standards compliance:** each phase gets its own numbered plan file here before
  its multi-file work starts; check items off immediately; keep Status current; never delete
  skipped items — leave unchecked and explain here.
- **Standards note:** the source doc mentions both `ingenium/ai-plans` and
  `ai-implementation-plan/`; the "Summary of Key Rules" table specifies
  `ai-implementation-plan/` at project root, which is used here. Flag if the other path is intended.
- **Ambiguities to confirm before/at Phase 2:** whether `Tags` becomes its own table now or
  stays text (PHASE2 note), and whether `DnsEndpointId` is nullable (assumed yes).
- Design docs as they were named at kickoff: `argus ai plan.txt` (technical),
  `beginnerguide.txt`, `PREHLED-projektu-CZ.txt`. Only the last one still exists under that
  name; the brief is `roadplan`, and the superseded normalization plan sits in
  `docs/historie/`.
