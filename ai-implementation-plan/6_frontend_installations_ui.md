# Plan: Frontend — Installations UI (Phase 4)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** React + TypeScript + Fluent UI on Vite: an installations grid with search,
filtering, sorting and paging, plus a detail/edit dialog where every lookup is an Id-backed
dropdown rather than free text — the UI enforcement of the normalized model.

---

## Checklist

- [x] Vite + React 19 + TypeScript app in `apps/frontend`, registered in NX via `project.json`
- [x] Vite dev proxy `/api` → `http://localhost:5080` (same-origin in dev)
- [x] `api/types.ts` mirroring the backend DTOs
- [x] `api/client.ts` — typed fetch wrapper, bearer token, `ApiError` from `ErrorResponse`
- [x] `auth/AuthContext.tsx` — token in localStorage, revalidated against `/auth/me` on boot
- [x] `hooks/useLookups.ts` — loads all five lookups in parallel _(ten today, and the kind list
      now comes from `GET /api/lookups` rather than being hard-coded here)_
- [x] `pages/InstallationsPage.tsx` — grid, debounced search, 3 facet filters, sortable columns, paging, delete
- [x] `components/InstallationDialog.tsx` — create/edit form, all lookups as dropdowns
- [x] `pages/LookupsPage.tsx` — management screens for all five lookups (Phase 6 item, done here) _(ten today)_
- [x] `App.tsx` shell with tab navigation and sign-out
- [x] `tsc -b && vite build` passes clean

---

## Notes

- **Dropdowns submit Ids, never names.** `InstallationUpsertDto` on the server only accepts
  Ids, so an invented machine name cannot enter through the UI at all.
- **DNS endpoint dropdown offers "(none)"** because `DnsEndpointId` is nullable — a worker
  installation has no public endpoint. Every other lookup is required.
- **Search is debounced 250 ms**; a filter change resets to page 1, since staying on page 7 of
  a smaller result set shows an empty grid.
- **Delete asks for confirmation** and is a soft delete server-side; deleting a lookup that
  installations still reference fails with a readable message from the API.
- `react-router-dom` is installed but the shell currently uses simple tab state — two screens
  did not justify routes. Left in `package.json` for when the screen count grows.
- The bundle is ~628 kB (181 kB gzipped) — Fluent UI is large. Acceptable for a demo; code
  splitting is the obvious first optimization if it ever matters.
