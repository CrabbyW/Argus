# Plan: Deployability & Polish (Phase 6)

**Date:** 2026-07-29
**Status:** `Complete` — 2026-07-30, except container images, which stay out of scope by the
decision recorded in Notes. Test and CI wiring was added on 2026-07-30; see the README.
**Description:** Make Argus runnable from a documented single path, keep all configuration and
secrets out of source, and finish the remaining polish items (lookup management screens,
end-to-end filtering/sorting/paging).

---

## Checklist

- [x] Lookup management screens for all five lookups (`LookupsPage.tsx` + `LookupsController`)
- [x] Filtering / sorting / pagination end-to-end (UI → filter DTO → EF query → paged response)
- [x] MSSQL running locally on SQL Server LocalDB _(replaced `docker-compose.yml` on 2026-07-29 — see `2_environment_setup.md`)_
- [x] Config externalized: `appsettings*.json` and `secrets/` gitignored, `appsettings.Example.json` committed
- [x] Root `package.json` scripts (`db:up`, `dev`, `build`, `db:migrate`)
- [x] NX targets for both apps (`nx run argus-api:serve`, `nx run argus-frontend:serve`)
- [x] README with the full run steps
- [x] Backend builds clean; frontend type-checks and builds clean
- [x] Verify the running stack end-to-end (login → grid → create → edit → delete) _(verified against the API on 2026-07-29; browser click-through completed 2026-07-30 — see `9_frontend_ux_overhaul_and_fixes.md`)_
- [ ] Container images for the API and frontend _(out of scope — see Notes)_

---

## Notes

- **Docker is not used anywhere in this project.** It hosted only the database and its WSL2 VM
  cost over half the machine's RAM, so it was replaced by SQL Server LocalDB on 2026-07-29.
  The documented run path is `pnpm run db:up` (LocalDB) plus `pnpm dev` for the apps.
- **Container images stay out of scope.** Packaging is a decision for the first real deployment,
  and building images that were never once run would be guesswork.
- **The API migrates and seeds on startup** (`Database:MigrateAndSeedOnStartup`), so the
  documented path is genuinely one command per component. Turn this off for any environment
  where schema changes should be applied deliberately.
- **Demo credentials are demo credentials.** `msfadmin` / the value of `Seed:AdminPassword`.
  Change it before this is reachable by anyone else, and note that changing the setting alone is
  not enough once the database exists — see the README's pre-deployment checklist.
