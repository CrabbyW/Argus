# Plan: NX Monorepo Skeleton (Phase 1)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** Set up the NX monorepo managed by pnpm that hosts both the .NET backend and
the React frontend, so both halves of Argus live in one repository with one task runner.

---

## Checklist

- [x] Root `package.json` with pnpm as package manager and NX dev-dependency
- [x] `pnpm-workspace.yaml` declaring `apps/*` and `libs/*`
- [x] `nx.json` with cache/target defaults
- [x] `.gitignore` covering node_modules, bin/obj, `appsettings*.json`, `secrets/`
- [x] `apps/netcorebackends/` for the .NET solution (backend app registered via project.json)
- [x] `apps/frontend/` for the Vite React app (registered via project.json)
- [x] `libs/` reserved for shared code (currently the generated API types live in the frontend)
- [x] `Directory.Build.props` centralizing assembly metadata per `CLAUDE-dotnet.md`

---

## Notes

- The backend is registered in NX through a `project.json` that shells out to `dotnet`
  (`nx run argus-api:build|serve`), rather than an NX .NET plugin — fewer moving parts and
  no dependency on a third-party plugin's release cadence.
- `libs/` is created but intentionally empty for now; shared TS types are generated into the
  frontend. If a second frontend appears, they move to `libs/api-types`.
