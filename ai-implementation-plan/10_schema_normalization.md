# Plan: Complete Implementation — Nine Lookups, Installation as Ids Only

**Date:** 2026-07-30
**Status:** `Complete` — verified on Windows 2026-07-31. See §9 for what was run and one bug it
caught.
**Description:** The roadplan requires **nine** shared values, each its own lookup table, filled
*before* the installation table exists — so that `ApplicationInstallations` is a row of foreign
keys plus its own dates and flags, exactly as the reference spreadsheet shows. The database
model, the API and the backend test suite have been rebuilt to that shape. **The frontend has
not been touched and no longer compiles or talks to the API.** This plan takes the project from
that half-state to a complete, verified implementation.

This file supersedes `plan.txt`, which describes a migration strategy that no longer exists.
See §0. Every `plan.txt` below refers to that file; it was moved out of the repository root on
2026-08-03 and now lives at `docs/historie/plan-normalizace-prekonany.txt`, kept only as a record
of the reasoning.

---

## 0. Ground truth — audit taken 2026-07-30, before planning

Everything below was read out of the working tree, not assumed. Where `plan.txt` or
`progress.txt` disagree, **this section wins**.

### 0.1 The migration history was collapsed

`plan.txt` is built around a hand-written, data-preserving migration
`20260730152501_NormalizePathsTagsAndRepositories`, with backups, `STRING_SPLIT` back-fill and
`THROW` guards. **That file is gone.** So are the two migrations it built on
(`20260729100928_InitialCreate`, `20260730091040_FilterDeploymentUniqueIndexToLiveRows`).

What exists now is a single clean migration: **`20260730194553_InitialCreate`**, creating all
thirteen tables in one step.

Consequences, and they are not optional:

- **There is no upgrade path from an existing dev database.** The only way forward is to drop
  the database and let `InitialCreate` + seed rebuild it.
- Every back-fill risk in `plan.txt` — `STRING_SPLIT` compatibility level, `LTRIM(RTRIM(...))`
  collisions, `THROW 50001/50002/50004`, the lossy `Down()`, the `__*Backup_Phase2` tables —
  **no longer applies**. Do not spend time rehearsing it.
- The new risk in its place is the plain one: **dropping the dev database destroys anything
  hand-entered into it.** §2 handles that.

### 0.2 Entities were renamed

| Old | New | Table |
|---|---|---|
| `Application` | `AppName` | `AppNames` |
| `AppStage` | `AppStageName` | `AppStageNames` |
| `Installation` | `ApplicationInstallation` | `ApplicationInstallations` |

The names now match the roadplan and the spreadsheet tabs. The unique index is
`UX_ApplicationInstallations_Deployment`.

### 0.3 What is genuinely done

- **Model** — 13 tables. 8 plain lookups + `AppRepositories` + `ApplicationInstallations` +
  `InstallationTags` + `InstallationRepositories` + `ApplicationUsers`.
- **API** — `LookupKind` has all nine members; `LookupRegistry` has nine descriptors;
  `InstallationUpsertDto` accepts Ids only; `AppRepositoryService` is M:N.
- **Tests** — 37 tests, already rewritten onto the new shape (`TestDb` seeds the new lookups and
  has `RootPathIdAsync` / `PhysicalPathIdAsync` find-or-create helpers).

### 0.4 What was not done at the time of the audit

- **The entire frontend.** It was on the old contract in every file listed in §4. Two lookup
  routes it called (`applications`, `appstages`) no longer exist and would 404.
- **Four test gaps**, including the one `plan.txt` itself marked MUST-HAVE (§3).
- **Nothing has been compiled or run since the rename.** No `dotnet build`, no `pnpm run test`,
  no `pnpm run build`. The .NET SDK is not present in this environment — every build and test
  command in this plan must be run on the Windows machine.
- **Documentation** across six files still described the five-lookup demo.

### 0.6 Session of 2026-07-30 — what was written, and what remains unproven

**Written:** the whole of §3 (two new test files), §4 (all six frontend files) and §5
(documentation). Boxes are ticked accordingly.

**Not run, and this is the load-bearing caveat:** the environment this work was done in has no
.NET SDK, and its package registry is unreachable, so `pnpm install` could not restore the
frontend's dependencies. Neither `dotnet build`, `pnpm run test`, `pnpm run build` nor
`pnpm run lint` has been executed against any of it.

What was verified mechanically instead, by script rather than by eye:

- Every field of `InstallationDetail`, `InstallationListItem`, `InstallationUpsert`,
  `InstallationFilter`, `AppRepository` and `AppRepositoryUpsert` matches the corresponding C#
  DTO **exactly** — no missing key, no extra key, on either side.
- The nine `LookupKind` strings match `Enum.GetNames<LookupKind>()` lowercased.
- Every `lookups.x` reference resolves to a field of the `Lookups` interface.
- Every `api.x(...)` call resolves to a method on the client.
- Every `styles.x` reference resolves to a key of that file's `makeStyles` block.
- Every identifier imported from `api/types` is exported by it.
- Every sortable grid column maps to a column `InstallationService.ApplySort` accepts.
- No occurrence of the retired identifiers (`applications`, `appstages`, `applicationId`,
  `appStageId`) survives anywhere in `src`.
- Braces, parens and brackets balance in all sixteen frontend files.

That covers contract drift, which is the failure mode this change is prone to. It does **not**
cover type errors within a file, React hook rules, Fluent component prop signatures, or anything
at runtime. §2 and §6 remain the real gate and neither has been started.

### 0.5 Where the dates live — answering an open question

`ValidFromDate`, `ValidToDate`, `IsActive`, `IsEnabled`, `CreatedUtc`, `ModifiedUtc` sit **on
`ApplicationInstallations` itself** and are correct there. They are not shared values, so they
are not lookups: two installations of the same app on the same machine have their own validity
windows. `ValidFromDate` is `date NOT NULL`, `ValidToDate` is `date NULL` (null = still valid).

Already implemented and covered: overlap-based date filtering
(`InstallationService.cs:99-109` — an installation matches when its window *overlaps* the
requested one, not merely when it starts inside it), `ValidToDate < ValidFromDate` → 400
(`:375`), and `ValidFromDate` defaulting to today when omitted (`:201`). Tests
`The_date_filter_matches_on_overlap_not_containment` and
`An_end_date_before_the_start_date_is_rejected` cover both.

The only outstanding date work is on the frontend: the grid has no date-range facet, and
`includeDisabled` is not exposed at all — which together make the roadplan's "what was here
last quarter?" question unanswerable from the UI even though the API answers it. §4.4.

---

## 1. The nine lookups

| # | Lookup | Route | Editable via Lookups screen | Notes |
|---|---|---|---|---|
| 1 | Machines | `machines` | yes | |
| 2 | AppNames | `appnames` | yes | **route renamed** from `applications` |
| 3 | AppStageNames | `appstagenames` | yes | **route renamed** from `appstages`; has `SortOrder` |
| 4 | ProcessorArchitectures | `processorarchitectures` | yes | |
| 5 | DnsEndpoints | `dnsendpoints` | yes | has `IsLoadBalancer` |
| 6 | RootPaths | `rootpaths` | yes | **new** |
| 7 | PhysicalPaths | `physicalpaths` | yes | **new**, names up to 512 |
| 8 | Tags | `tags` | yes | **new**, M:N via `InstallationTags` |
| 9 | AppRepositories | `apprepositories` | **read-only here** | M:N via `InstallationRepositories`; writes go through `/api/apprepositories` |
| 10 | RepositoryTypes | `repositorytypes` | yes | **added 2026-07-31**, after this plan was written — see §10 |

`AppRepositories` is registered `IsReadOnly = true` in `LookupRegistry` on purpose:
`LookupUpsertDto` has nowhere to put `RepositoryType` or the installation links, so an ordinary
read-modify-PUT through the generic endpoint would reset the type to `Unknown` and drop every
link. Reading is fine — a dropdown of repositories is the same query as any other kind. The test
`Repositories_are_readable_but_not_writable_through_the_lookup_api` pins this.

### The ordering rule

The roadplan: *"Nezakládá se dřív, než jsou číselníky naplněné — výběr v UI je vždycky dropdown,
nikdy volný text."* The spreadsheet shows the same: `Machines` / `AppNames` / `AppStageNames`
are `ID | Name`; `ApplicationInstalation` is `ID | MachineId | AppNameId | AppStageNameId | …`.

This is enforced in three places and must stay enforced in all three:

1. `InstallationUpsertDto` accepts **no names**, only Ids — a client cannot invent a machine.
2. `ValidateReferences` rejects an Id that does not exist (400, not a FK violation at 500).
3. `DbSeeder` seeds lookups → installations → repositories, in that order.

---

## 2. Phase 1 — Get the backend green and the database rebuilt

**Nothing in §3–§5 may start before this phase is fully checked.** A red build makes every
later error ambiguous.

- [x] `dotnet build` on `Argus.slnx`. Expect **0 errors, 0 warnings**. This is the single
      highest-value step in the plan: the rename in §0.2 means every stale reference is a
      compile error pointing exactly at the site that needs a change.
- [x] `dotnet ef migrations list` — confirm `20260730194553_InitialCreate` is the only entry
      and that no orphan `__EFMigrationsHistory` rows reference the three deleted migrations.
- [x] **Before dropping anything:** export whatever is in the current dev database that is not
      seed data. If the answer is "nothing", write that down here and move on — but check, do
      not assume. `progress.txt` records that the dev database was cleaned to exactly the seed
      set on 2026-07-30, so this is expected to be a no-op.
      **Checked, and it was a no-op.** The database still held the *old* eight-table schema
      (`Applications`, `AppStages`, `Installations`) and `__EFMigrationsHistory` contained exactly
      the two deleted migrations — confirming §0.1's orphan-row concern. All five installation
      rows shared one `CreatedUtc` (2026-07-29 17:12:45) with `ModifiedUtc` null: a single seed
      transaction, nothing hand-entered. A backup was taken anyway before the drop.
- [x] `dotnet ef database drop --force`, then `dotnet ef database update`.
      _(`Database:MigrateAndSeedOnStartup` is `true` at `Program.cs:123`, so `pnpm run dev:api`
      would do this too — do it explicitly so the output is visible.)_
- [x] Verify the seed produced **exactly** these counts. Any deviation is a bug, not a variance:

      | Table | Rows |
      |---|---|
      | Machines | 4 |
      | AppNames | 3 |
      | AppStageNames | 5 |
      | ProcessorArchitectures | 3 |
      | DnsEndpoints | 2 |
      | RootPaths | 3 |
      | PhysicalPaths | 4 |
      | Tags | 4 |
      | ApplicationInstallations | 5 |
      | InstallationTags | 9 |
      | AppRepositories | 3 |
      | InstallationRepositories | **7** |
      | ApplicationUsers | 1 |

      _`InstallationRepositories` = 7, not the 8 claimed in `plan.txt`: two CallCenter
      repositories × 3 CallCenter installations, plus one Bitbucket repository × 1 Extranet
      installation. Recount from `DbSeeder.SeedRepositoriesAsync` if this ever disagrees._
- [x] `pnpm run test` — 37 tests green. _(Actually **60** at this point: the suite had grown past
      what §0.3 recorded. 68 after §3.)_
- [x] `pnpm run dev:api`, then smoke the API by hand before any frontend work:
  - [x] `GET /api/health` → `Healthy`
  - [x] `POST /api/auth/login` with `msfadmin` → token
  - [x] `GET /api/lookups/appnames` → 200 _(the old `applications` must 404 — confirm it does,
        so §4.1 is provably necessary)_
        **It returns 400, not 404** — the route binds `{kind}` to the `LookupKind` enum, and an
        unparseable value fails model binding before reaching the handler. Rejected either way, so
        §4.1 was necessary; the expectation in this line was simply wrong about which code.
  - [x] `GET /api/lookups/rootpaths`, `physicalpaths`, `tags`, `apprepositories` → 200
  - [x] `GET /api/installations?dnsEndpointId=<paha>` → the load-balancer case: SERVER1 **and**
        GAIIS1. This is the headline question from the roadplan.
  - [x] `GET /api/installations?tagId=<web>` → `totalCount` is the number of *installations*,
        not the number of tag links (§3, first test).

---

## 3. Phase 2 — Close the test gaps

37 tests exist. Four things `plan.txt` called for are missing, and one of them is the regression
guard for the most expensive mistake in this codebase.

- [x] **`Searching_by_tag_does_not_multiply_the_row_count`** — MUST HAVE.
      One installation tagged `{web, webhook}`; search `"web"`; `TotalCount` must be `1`.
      `CountAsync` runs the same query as the page query before paging, so rewriting the tag
      predicate from `.Any()` to a join silently inflates every count in the UI. There is
      currently nothing stopping that rewrite.
- [x] Tag links round-trip: editing tags adds and removes only what changed (`SyncLinks` diff).
- [x] Duplicate Ids in `TagIds` / `RepositoryIds` produce one link, not a PK violation
      _(guards the `.Distinct()` in `SyncLinks`)_.
- [x] `AppRepositoryServiceTests` — this file does not exist:
  - [x] a repository added to one installation does **not** appear on a sibling installation of
        the same application _(this is the whole point of the M:N change)_
  - [x] the same URL shared by two installations is **one** row in `AppRepositories`
  - [x] `GetAllAsync(installationId, appNameId)` filters on each argument independently
- [x] Filter by `tagId` returns only installations carrying that tag.
- [x] Filter by `repositoryId` returns only installations linked to that repository
      _(no test touches this facet today)_.
- [x] `includeDisabled = true` returns soft-deleted rows; default `false` hides them.
- [x] A lookup name longer than that kind's `MaxNameLength` → 400, not `SqlException`
      _(the per-kind check is newer than the tests; only `Every_lookup_declares_a_maximum_name_length`
      touches it, and that is a structural test, not a behavioural one)_.
- [x] `pnpm run test` — target ~48 tests green. _(**68** green.)_

**Standing limitation, do not forget it:** `TestDb` builds its schema with `EnsureCreated()` on
SQLite. **Migrations never run in tests.** Nothing in xUnit proves the SQL Server migration is
correct; only §2 does. Any future hand-written SQL needs a real-server rehearsal.

---

## 4. Phase 3 — Rebuild the frontend onto the new contract

This is the bulk of the remaining work: ~2,750 lines across 16 files, of which the six below
carry the contract. Work in the order given — types first, then the shared hook, then screens —
so that `tsc` narrows the error list at each step instead of drowning it.

### 4.0 Contract diff — the authoritative mapping

| Concern | Frontend today | Backend now |
|---|---|---|
| lookup route | `'applications'` | `'appnames'` — **404 today** |
| lookup route | `'appstages'` | `'appstagenames'` — **404 today** |
| lookup routes | — | add `'rootpaths'`, `'physicalpaths'`, `'tags'`, `'apprepositories'` |
| detail Id | `applicationId` | `appNameId` |
| detail Id | `appStageId` | `appStageNameId` |
| root path | `rootPath: string` (free text) | `rootPathId: number` + resolved `rootPath: string` |
| physical path | `physicalPath: string` (free text) | `physicalPathId: number \| null` + resolved `physicalPath` |
| tags (list row) | `tags?: string \| null` | `tags: string[]` |
| tags (detail) | `tags?: string \| null` | `tags: LookupItem[]` |
| tags (write) | `tags: string` | `tagIds: number[]` |
| repositories (write) | — | `repositoryIds: number[]` |
| repository shape | `applicationId: number` | `installationIds: number[]` |
| repository query | `?applicationId=` | `?installationId=` and/or `?appNameId=` |
| detail payload | — | `appRepositories: AppRepositoryDto[]` now included |
| lookup name length | implicit | up to 512 (PhysicalPaths); real limit is per-kind |

### 4.1 `src/api/types.ts`

- [x] `LookupKind` → all nine route strings above
- [x] `InstallationListItem.tags` → `string[]`
- [x] `InstallationDetail`: `applicationId`→`appNameId`, `appStageId`→`appStageNameId`, add
      `rootPathId`, `physicalPathId`, `tags: LookupItem[]`, `appRepositories: AppRepository[]`
- [x] `InstallationUpsert`: Ids only + `tagIds: number[]` + `repositoryIds: number[]`
- [x] `InstallationFilter`: add `rootPathId`, `physicalPathId`, `tagId`, `repositoryId`,
      `validFrom`, `validTo`, `includeDisabled`
- [x] `AppRepository`: drop `applicationId`, add `installationIds: number[]`
- [x] `AppRepositoryUpsert`: drop `applicationId`, add `installationIds: number[]`

### 4.2 `src/api/client.ts`

- [x] `getRepositories({ installationId?, appNameId? })`
- [x] Confirm every query-string key matches `InstallationFilterDto` **exactly** — ASP.NET binds
      by name and silently ignores a misspelled facet, so a typo here is a filter that appears
      to work and quietly returns everything.

### 4.3 `src/hooks/useLookups.ts`

- [x] Nine fields, nine parallel `GET`s _(eight editable lookups + repositories, which the
      installation dialog needs for its multiselect)_
- [x] Keep the existing "lookups failed to load" error surface — silently empty dropdowns were
      a fixed bug on 2026-07-29 and must not come back

### 4.4 `src/pages/InstallationsPage.tsx` (654 lines)

- [x] Facets: Root path, Physical path, Tag, Repository, plus the date range and
      "include decommissioned" toggle. That is a lot of controls — group them, do not simply
      append; the filter card was deliberately designed on 2026-07-29 and should stay legible
- [x] `readFilter` / `writeFilter`: new query-string keys (`root`, `ppath`, `tag`, `repo`,
      `from`, `to`, `disabled`). Filters live in the URL so a filtered view can be sent to a
      colleague and Back works — keep that property
- [x] Tags cell renders one `Badge` per entry; widen the column from 100
- [x] Verify every sortable column header still maps to a column `InstallationService` accepts
      _(unknown columns silently fall back to machine name — `An_unknown_sort_column_falls_back_to_machine_name`)_

### 4.5 `src/components/InstallationDialog.tsx` (292 lines)

- [x] Blank form, hydration and payload all onto Ids
- [x] **Root path and Physical path: freeform `Combobox` with type-ahead**, not a bare dropdown.
      When the typed value matches no existing item, the dialog `POST`s it to
      `/api/lookups/rootpaths` (or `physicalpaths`) and uses the returned Id. Handle the
      `400 "... already exists."` race by reloading the lookup and matching by name.
      _Rationale: today the user simply types a path. Forcing them to visit the Lookups screen
      first would be a usability regression, and the roadplan's dropdown rule exists to prevent
      duplicate free text — which find-or-create satisfies exactly._
- [x] Tags: multiselect `Dropdown` over the Tags lookup
- [x] Repositories: multiselect over the repositories lookup _(currently read-only display)_
- [x] **Delete the hint** `"Free text for now; becomes its own table in PHASE2."` — PHASE2 is now
- [x] Client-side guard on `ValidToDate >= ValidFromDate` so the user is told before the round
      trip; the server check at `InstallationService.cs:375` stays as the real one

### 4.6 `src/pages/LookupsPage.tsx` (372 lines)

- [x] `tabs` (lines 67-73): rename two kinds, add three. Eight tabs
- [x] Eight tabs will not fit a narrow window → `overflowX: auto` on the tab strip
- [x] Per-kind `maxLength` on the name field, mirroring the server's `MaxNameLength(kind)`
- [x] The delete-blocked message ("Installations still pointing at it will block this") is now
      also reachable for tags and paths — confirm the wording still reads correctly for them

### 4.7 `src/components/InstallationDetailDialog.tsx` (164 lines)

- [x] Tags as badges
- [x] Repositories read from `detail.appRepositories` instead of a second request

### 4.8 `src/pages/RepositoriesPage.tsx` (392 lines)

- [x] Filter by installation alongside filter by application; Installation column; installation
      multiselect in the dialog
- [x] **The comment on lines 64-68 currently asserts the exact opposite of the new model.**
      Rewrite it — a stale comment that confidently states the wrong ownership is worse than no
      comment

### 4.9 Frontend gate

- [x] `pnpm run build` — `tsc` clean. Until this passes, none of the above is verified
- [x] `pnpm run lint` clean _(eslint config is new as of 2026-07-30 — it must actually run, not
      silently no-op as `pnpm run lint` did before that date)_

---

## 5. Phase 4 — Documentation

Six files still describe the five-lookup demo. Each of these is a specific false statement:

- [x] `ai-implementation-plan/4_ef_core_model_and_migration.md:26` — the analysis table
- [x] `ai-implementation-plan/4_ef_core_model_and_migration.md:54-55` — "Tags stays a plain string"
- [x] `ai-implementation-plan/9_frontend_ux_overhaul_and_fixes.md:129` — "Tags remain free text"
- [x] `README.md:184` — "Tags is still free text", plus the data-model section
- [x] `progress.txt` — "= celkem 8 tabulek", "Tags: zůstává text", "5 číselníků"
- [x] `PREHLED-projektu-CZ.txt` — the lookup list and the closing summary
- [x] `plan.txt` — **delete or mark superseded.** It documents a migration that no longer exists
      (§0.1). Leaving it in the repo root, where it looks like the current plan, is a trap for
      whoever reads this project next
- [x] Record the LocalDB deviation once, in `README.md`, and stop repeating it: LocalDB cannot do
      SQL authentication, so the connection is `Trusted_Connection`. The roadplan's SQL-auth
      requirement assumed a container

---

## 6. Phase 5 — End-to-end verification

Run in this order, on a database freshly rebuilt per §2.

- [x] `dotnet build` — 0 errors, 0 warnings
- [x] `pnpm run test` — all green
- [x] `pnpm run build` + `pnpm run lint` — clean
- [x] `pnpm run db:up` → `dev:api` → `dev:frontend`, all three up

**Browser walkthrough.** Every item is a question the roadplan says Argus must answer:

- [x] Log in as `msfadmin`; a wrong password shows an error, not a blank page
- [x] Grid loads 5 rows with tag badges
- [x] *"Which machines serve paha.ga.local?"* — filter by that DNS endpoint → SERVER1 and GAIIS1
- [x] *"Where does RC0 of CallCenter run?"* — filter app + stage
- [x] *"What else is on GAIIS1?"* — filter by machine
- [x] Search a tag name; **record count is not inflated** (the §3 regression, seen through the UI)
- [x] Filter by root path, by physical path, by repository — each changes the result
- [x] Date range filter answers "what was installed during this window?"; toggling
      "include decommissioned" brings soft-deleted rows back into view
- [x] Create an installation choosing an **existing** root path from the combobox
- [x] Create one **typing a new** root path — it is created as a lookup row and reused, not
      duplicated. Check the Lookups screen afterwards
- [x] Edit tags on an installation: add one, remove one; both persist
- [x] Add a repository to one installation; confirm it is **absent** from a sibling installation
      of the same application
- [x] Delete is soft: the row disappears from the grid but survives with `IsEnabled = 0`
- [x] Reinstall the same app+stage+path on the same machine after decommissioning it — must
      succeed _(the unique index is filtered to live rows; `The_unique_index_ignores_decommissioned_rows`)_
- [x] All eight lookup screens read and edit
- [x] Rename a machine → the new name appears on every installation immediately. **This is the
      entire justification for the data model; if it fails, nothing else matters**
- [x] Rename a DNS endpoint → `IsLoadBalancer` survives _(regression fixed 2026-07-29, pinned by
      `Renaming_a_dns_endpoint_keeps_the_load_balancer_flag`)_
- [x] Rename a stage → `SortOrder` survives
- [x] Try to delete a tag, root path and machine that are in use → blocked with a clear message
- [x] Browser console clean apart from an expected 401 from the wrong-password test
- [x] Swagger `/swagger`: `InstallationUpsertDto` shows `rootPathId`, `physicalPathId`,
      `tagIds`, `repositoryIds` and **no** name fields
- [x] Reset the database to a clean seed afterwards, and re-verify the §2 counts

---

## 7. Not in scope, deliberately

Named here so nobody re-opens them mid-flight: provisioning or deploying anything; availability
or health monitoring; auto-discovery of installations; deployment beyond localhost; Docker
(removed on 2026-07-30 — Docker Desktop consumed over half the machine's memory for a database
alone, replaced by LocalDB); the generated TypeScript client from `[EndpointName]` (the
attributes are all in place, so it can be wired up later without touching controllers); real
production data.

---

## 8. Risks, ranked

1. **The frontend rewrite is large and entirely uncompiled.** ~2,750 lines against a contract
   that changed in fifteen places. Mitigation: §4.0 is the authoritative diff; work types-first
   so `tsc` acts as the checklist; do not start until §2 is green.
2. **`MigrateAndSeedOnStartup = true`** means `dotnet run` alone rebuilds the schema. Combined
   with §0.1 — no upgrade path from an old database — an unthinking `pnpm run dev:api` against a
   stale database is the most likely way to lose hand-entered data. Check §2 first.
3. **Row multiplication in `CountAsync`** if anyone rewrites the tag or repository predicate as a
   join. Nothing guards this today. Fixed by the first test in §3 — write it early.
4. **Migrations are untested by xUnit** (`EnsureCreated()` on SQLite). Only §2 exercises the real
   schema.
5. **`PhysicalPaths` as a shared lookup is semantically imperfect.** Two installations on
   *different machines* legitimately share `c:\inetpub\callcenter.rc0`; after normalization that
   is one row, so renaming it on the Lookups screen rewrites the path on both machines even
   though they are different disks. The table also grows roughly 1:1 with installations. The
   roadplan lists `PhysicalPaths` as its own lookup, so this is **accepted, not designed away**;
   the type-ahead combobox in §4.5 keeps it from being painful. Revisit only if it bites.
6. **Silent query-string binding.** A misspelled facet key is ignored by ASP.NET, producing a
   filter that looks functional and returns everything. Caught only by the §6 walkthrough —
   which is why each facet is checked individually there.
7. **Stale documentation actively misleads.** Six files and `plan.txt` currently state the
   opposite of the implemented model. §5 is not cosmetic.

---

## Notes

- **Why `AppRepositories` is M:N rather than a plain FK on the installation.** The roadplan lists
  repositories as an attribute of the installation. A plain foreign key would have multiplied
  rows (3 → 8) and stored the same URL several times — precisely the duplication the rest of this
  work removes. Decided with the user, 2026-07-30.
- **Why link tables have no `IsEnabled` and no query filter.** A filtered link table can hide a
  row from the edit diff, which then re-inserts it and violates the primary key. Soft delete
  lives on the ends of a relationship, never on the relationship itself.
- **Why `EnsureNotInUseAsync` for tags queries `db.Installations` and not `db.InstallationTags`.**
  Only the former applies the `IsEnabled` query filter, so a decommissioned installation does not
  block deleting a tag. Pinned by
  `A_tag_used_only_by_a_decommissioned_installation_can_be_deleted`.
- **Why the deployment unique index is filtered to `[IsEnabled] = 1`.** Decommissioning is a soft
  delete; without the filter a retired row keeps its slot forever and reinstalling the same thing
  — an ordinary event in an inventory, and exactly what `ValidFromDate`/`ValidToDate` exist to
  record — fails on a constraint the user can neither see nor clear.
- **Build commands must run on Windows.** The .NET SDK and pnpm are not available in the
  environment this plan was written in; every `dotnet` and `pnpm` step above is unverified here
  by necessity, not by oversight. _Superseded on 2026-07-31 — all of it has now been run; see §9._

---

## 9. Verification run — 2026-07-31, on Windows

The caveat in §0.6 is discharged. .NET 10.0.302, Node 24, pnpm 11.17 were all present, so every
command the plan defers to a Windows machine was executed.

### What passed

| Gate | Result |
|---|---|
| `dotnet build` (`Argus.slnx`) | 0 errors, 0 warnings |
| `pnpm run test` | **68** passed, 0 failed |
| `pnpm run build` (`tsc -b` + vite) | clean — the §8 risk #1 rewrite compiles |
| `pnpm run lint` | clean |
| `dotnet ef migrations list` | `20260730194553_InitialCreate` only |
| Seed counts | all thirteen exact, `InstallationRepositories` = 7 as corrected in §2 |
| Swagger `InstallationUpsertDto` | Ids only — `rootPathId`, `physicalPathId`, `tagIds`, `repositoryIds`, no name fields |

Every §6 behaviour was exercised against the running API and passed: the load-balancer query
(`paha.ga.local` → SERVER1 **and** GAIIS1), each facet narrowing independently, find-or-create of a
new root path including the duplicate-name race, tag add/remove and duplicate collapse, soft delete
then reinstall on the freed slot, machine rename propagating to every installation, `IsLoadBalancer`
and `SortOrder` surviving a rename, in-use lookups blocked from deletion, and `AppRepositories`
refusing writes through the generic lookup route. The browser walkthrough was driven headless over
CDP: login and rejected password, the grid with tag badges, all eight lookup tabs with correct row
counts, the detail dialog, and the edit dialog hydrating from Ids. Browser console clean; the only
4xx was the intended 401 from the wrong-password test.

### The bug this caught

**Deep-linked filters were silently discarded.** `App.tsx:121` redirected `/` to `/installations`
with `<Navigate to="/installations">`, which drops the query string. Opening `/?machine=2` — or any
filtered view sent to a colleague, which §4.4 explicitly calls for — landed on an *unfiltered* grid
with the facets reset and no error anywhere. Nothing else could have caught it: the page's own
`readFilter`/`writeFilter` were correct, `tsc` and eslint were clean, and the UI→URL direction
worked, so only loading a URL from cold revealed it. Fixed by carrying `location.search` across both
redirects; re-tested with `?machine=2`, `?q=callcenter`, `?tag=1` and `?disabled=true`, each of which
now restores the controls and issues the matching API query.

This is worth recording as the general shape of the risk: §8 #6 warned that a mistyped facet key
fails silently, and this was the same failure mode one layer up — a filter that is dropped rather
than misread, with an unfiltered grid looking exactly like a correct one.

### Two things noted, not changed

- **`ILookupDescriptor.EntityType` was added.** The §3 name-length test needs each kind's real
  column width; without this the test would have had to hand-copy the kind→entity mapping, which is
  the drift `LookupHandler.MaxNameLength` exists to avoid. One property, implemented as
  `typeof(TEntity)`.
- **`installationIds` is truncated on the detail payload.** `GET /api/installations/{id}` returns
  each `appRepositories[]` entry with `installationIds` containing only the installation being
  viewed, where `GET /api/apprepositories` correctly reports all of them. Harmless today — the
  detail and edit dialogs read only `id`, `repositoryUrl` and `repositoryType` — but it is a live
  trap: anything that round-trips a repository from a detail payload into a `PUT` would silently
  unlink it from every sibling installation. Left alone because fixing it is a projection change
  with no current consumer. **Fixed 2026-07-31** (commit `2c60aea`): the detail projection now
  reports every installation the repository serves, same as the list endpoint.

---

## 10. The tenth lookup — `RepositoryTypes`, 2026-07-31

Written up after the fact. The work was a single-session follow-on to this plan rather than a new
phase, so it is recorded here instead of in a plan file of its own; that is a deviation from
`CLAUDE-planning-standards.MD` and it is named rather than hidden.

**What changed and why.** `RepositoryType` was a C# enum (`Unknown`, `Git`, `Svn`, `Bitbucket`,
`Mercurial`, `Tfs`) stored as an `int`. The roadplan lists it as a *column* of `AppRepositories`, which is what the
enum implemented — but it is a shared vocabulary like every other value in this model, and the
rule the whole schema rests on ("every shared value is its own table with an Id") has no exception
for short strings. An enum also means adding a repository kind is a code change and a deploy,
where the point of Argus is that operators maintain their own vocabularies.

- `RepositoryType` entity + configuration; `AppRepository.RepositoryTypeId` is **nullable** —
  the old `Unknown` member had no row to become, and "not recorded" is honestly a null.
- Migration `20260731211501_AddRepositoryTypesLookup`.
- Registered in `LookupRegistry` as a fully editable kind (route `repositorytypes`). It is the one
  kind an installation does not reference directly, so its in-use check runs over
  `AppRepositories` — `Usage.FromRepositories`.
- Total: **ten lookups, fourteen tables**.

**The registry refactor that came with it.** Adding the ninth lookup under the old shape meant
editing six `switch` statements in `LookupService`, and the audit in §0.4 is where that cost was
first felt. The layer was rewritten to `Services/Lookups/` — one `LookupDescriptor<TEntity>` per
kind holding its projection, ordering, upsert and usage query, plus the presentation strings and
which optional columns it has (`HasDescription`, `HasSortOrder`, `HasLoadBalancer`). The frontend
reads those from `GET /api/lookups` instead of keeping its own copy of the tab list. The tenth
lookup was therefore one descriptor, not six switch arms.

Descriptors are deliberately written against the **concrete** entity type, not `ILookupEntity`:
a projection built over the interface puts interface members into the expression tree, which EF
resolves only by name-matching. Concrete lambdas always translate to SQL.

- [x] Entity, configuration, migration
- [x] Descriptor registered; `LookupKind.RepositoryTypes` appended (existing numeric values unchanged)
- [x] Frontend type union and lookup hook pick it up from the served metadata
- [x] `dotnet build` 0/0, `pnpm run test` 79 green
