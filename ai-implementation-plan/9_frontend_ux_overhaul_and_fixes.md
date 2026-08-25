# Plan: Frontend UX Overhaul and Correctness Fixes

**Date:** 2026-07-29
**Status:** `Complete` — 2026-07-30. The browser click-through in Verification below was run
against the live stack (22 checks, all passing, including the load-balancer regression), and the
file deletion in section E is done.
**Description:** The app runs end to end, but a review against `roadplan` found one
data-corrupting bug, two silent-failure bugs, and a UI that does not behave like a website
(no URL routing, native `window.confirm` dialogs, no success feedback, no favicon). This plan
fixes correctness first, then makes the shell behave like a normal web app, then rebuilds the
Installations toolbar layout, then closes the gaps against `roadplan` (unused API filters,
AppRepositories management). Scope confirmed with the user on 2026-07-29: full scope,
including AppRepositories CRUD.

---

## Checklist

### A. Correctness — data loss and silent failures (do first)

- [x] Add `SortOrder` and `IsLoadBalancer` to `WebApiPoco/Common/LookupItemDto.cs`
- [x] Populate both fields in all five projections in `Services/LookupService.cs:20-48`
      (stages carry `SortOrder`, DNS endpoints carry `IsLoadBalancer`; others stay default)
- [x] Mirror the two fields on `LookupItem` in `apps/frontend/src/api/types.ts`
- [x] Fill the editor from the loaded row in `pages/LookupsPage.tsx:253-261` instead of the
      hardcoded `sortOrder: 0, isLoadBalancer: false`
- [x] Show Sort order / Load balancer as columns in the lookups table so the value is visible
      _(also fixed the hardcoded `colSpan={4}`, which no longer matched the column count)_
- [x] Reset the session on 401: `setUnauthorizedHandler` in `api/client.ts`, subscribed by
      `auth/AuthContext.tsx` _(not yet clicked through in a browser — see Notes)_
- [x] Surface `useLookups().error` in `pages/InstallationsPage.tsx:52` — today a failed lookup
      load silently yields empty dropdowns _(not yet clicked through in a browser)_

### B. Behave like a website

- [x] Introduce `react-router-dom` in `App.tsx`: `/installations`, `/lookups/:kind`,
      `/repositories`, plus a catch-all 404 page
- [x] Keep the Installations filter/sort/page state in the query string (`readFilter` /
      `writeFilter`), so a filtered view can be bookmarked, shared and reached via Back
- [x] Deep link the dialogs: `/installations/new`, `/installations/:id`, `/installations/:id/view`
- [x] Replace `window.confirm` with `components/ConfirmDialog.tsx` on all three pages
- [x] Add `Toaster` + `hooks/useAppToast.tsx` feedback for create / update / delete
- [x] Add a favicon (`apps/frontend/public/favicon.svg` + `<link rel="icon">`)
- [x] Add `components/ErrorBoundary.tsx` around the shell
- [x] Keep the table mounted while loading — dimmed with a corner spinner instead of unmounting
- [x] Make sortable headers real controls: focusable button, keyboard activation, `aria-sort`
- [x] Give Delete a destructive appearance distinct from Edit in all three tables

### C. Layout and visual pass

- [x] Split the single row into a page header (title + record count + Refresh + New) and a
      bordered filter card
- [x] Label every filter control with a `Field`, plus a "Clear filters" action
- [x] Consistent spacing/heading rhythm across Installations, Lookups and Repositories
- [x] Responsive filter grid (`repeat(auto-fit, minmax(170px, 1fr))`) and a sticky header
      _(built to reflow; not visually confirmed at a narrow width — see Notes)_
- [x] Dark mode in `main.tsx` following `prefers-color-scheme`, reacting to live changes

### D. Gaps against `roadplan`

- [x] Surface `processorArchitectureId`, `dnsEndpointId` and `isActive` as labelled facets.
      Verified: `?dnsEndpointId=1` (helpdesk.demo.example) returns BOREAS01 + BOREAS02 — the roadplan's
      headline question, answered by one dropdown
- [x] Show `PhysicalPath` in the grid — required adding it to `InstallationListItemDto` and
      `InstallationMapper.ToListItemDto`, which did not carry it
- [x] `components/InstallationDetailDialog.tsx` — read-only view with physical path,
      repositories and created/modified stamps
- [x] Backend: `AppRepositoriesController` + `AppRepositoryService` + `AppRepositoryUpsertDto`,
      reusing the existing `AppRepositoryDto` and `InstallationMapper.ToAppRepositoryDto`
- [x] Frontend: `pages/RepositoriesPage.tsx` — list, filter by application, create, edit,
      soft-delete

### E. Documentation and standards

- [x] Correct `2_environment_setup.md` — rewritten for LocalDB, status `Complete`
- [x] Clear Docker out of `1_argus_demo_build.md`, `4_ef_core_model_and_migration.md`,
      `8_deploy_packaging.md`
- [x] Fix the closing line of `progress.txt`, which still ordered "start Docker Desktop first"
- [x] Fix the database section and step 3 of `PREHLED-projektu-CZ.txt`
- [x] Record the LocalDB migration and the admin password reset — both are now written up in
      `2_environment_setup.md`, including the standards violation itself
- [x] Delete `docker-compose.yml` and `.env.example` _(done 2026-07-30. `.env.example` held only
      the compose password and nothing in the stack loads `.env`. The non-Windows path LocalDB
      cannot serve is now written up in the README instead of implied by a compose file.)_

---

## Verification

1. `pnpm run db:up`, `pnpm run dev:api`, `pnpm run dev:frontend`; sign in as `msfadmin` with the
   password in `Seed:AdminPassword`.
2. **Load-balancer regression (the bug that motivated this plan):** rename `helpdesk.demo.example`
   under Lookups → DNS endpoints, save, reopen. `IsLoadBalancer` must still be true. Confirm in
   SQL: `SELECT DnsName, IsLoadBalancer FROM DnsEndpoints`.
3. **Stage ordering:** edit any stage, save, then open the Stage dropdown — the order defined by
   `SortOrder` (`Services/LookupService.cs:33`) must be unchanged.
4. **Session expiry:** delete `argus.token` from localStorage, then act in the UI — the app must
   return to the login screen, not show an error banner.
5. **Routing:** filter the grid, copy the URL, open it in a new tab — same filtered view. Back
   button steps back through views. Refresh keeps the current page.
6. **Repositories:** create a repository on an application, confirm it appears in that
   application's installations detail view.
7. `pnpm run build` (`tsc -b && vite build`) and the API build must both pass clean.

---

## Notes

- **What is verified and what is not.** Everything checked above compiles (API: 0 warnings,
  0 errors; frontend: `tsc -b` clean, `vite build` clean) and every API-side change was
  exercised with real requests — including proving the lookup bug was real by sending the old
  payload, watching the flag drop, and restoring it. What has **not** happened is a human
  clicking through the UI in a browser; layout, dark mode and the toasts are unconfirmed
  visually.
- One `pnpm run build` run crashed with Windows `0xC0000409` (process abort). `tsc -b` and
  `vite build` each pass on their own and the combined command passed on retry — transient,
  not a code fault.
- The smoke test left one soft-deleted repository row (`git://server/test-argus.git`) in the
  database. It is invisible to the app by design; delete it in SQL if a spotless seed matters.
- **Root cause of the lookup bug is a DTO asymmetry, not a UI slip.** `LookupItemDto` (read)
  omits `SortOrder`/`IsLoadBalancer`, while `LookupUpsertDto` (write) requires them and
  `Services/LookupService.cs:124,140` assigns them unconditionally. The UI therefore cannot
  round-trip a value it was never sent. Fixing only the frontend would leave the same trap for
  the next caller, so the read DTO is fixed first.
- Section A is independent of B–D and can ship on its own; it is the only part that prevents
  ongoing data corruption.
- Item B's query-string state and item D's extra filters touch the same component. Doing B
  before D avoids reworking the filter wiring twice.
- Tags remained free text at the time of this plan (roadplan marked them `Tbd: PHASE2`).
  **No longer true:** on 2026-07-30 they became their own table with an M:N link, and the
  installation form now offers a multiselect. See `10_schema_normalization.md`.
