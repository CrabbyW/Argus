# Plan: REST API CRUD (Phase 3)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** Expose the Argus model over a JSON REST API following every convention in
`CLAUDE-dotnet.md`: `ApiResponse<T>` / `ErrorResponse` wrappers, `[EndpointName]` and
`[ProducesResponseType]` on every action, a lowercase `logger` static field, a service +
static mapper + filter-DTO layering, and a `GlobalExceptionHandlerMiddleware` so controllers
only try/catch business validation.

---

## Checklist

- [x] `WebApiPoco/Common/`: `ApiResponse<T>`, `ErrorResponse`, `DataViewFilterBase<T>`, `DataViewOutput<T>`, `LookupItemDto`, `LookupUpsertDto`
- [x] `WebApiPoco/Installations/`: list / detail / upsert / filter DTOs + `AppRepositoryDto`
- [x] `WebApiPoco/Auth/`: login request/response, current-user DTO
- [x] `Mappers/InstallationMapper` — static entity→DTO conversions
- [x] `Services/IInstallationService` + implementation (filter, sort, page, CRUD, soft delete)
- [x] `Services/ILookupService` + implementation (all five lookups, one service) _(five at the
      time; rewritten into the `Services/Lookups/` descriptor registry and grown to ten — plan 10 §10)_
- [x] `Middleware/GlobalExceptionHandlerMiddleware` (500 + traceId, no leaked details)
- [x] `InstallationsController` — GET list, GET by id, POST, PUT, DELETE
- [x] `LookupsController` — CRUD over `{kind}` for all five lookups _(ten kinds today)_
- [x] `HealthController` — anonymous liveness + DB reachability probe
- [x] Swagger/OpenAPI with a JWT bearer security definition
- [x] Backend compiles clean (`dotnet build`, 0 warnings, 0 errors)

---

## Notes

- **`[Authorize]` uses the JWT scheme only.** `CLAUDE-dotnet.md` shows
  `Negotiate,JwtBearer` because that template project supports Windows auth; `roadplan`
  specifies username + password for Argus, so Negotiate is deliberately not registered.
  The convention (explicit scheme on every controller) is followed.
- **DatexPushReceiver / HelpdeskPush sections of `CLAUDE-dotnet.md` do not apply** — those
  are push endpoints of the template project. Argus has no push receiver. Left unimplemented
  intentionally rather than inventing an Argus equivalent.
- **One `LookupsController` for five tables.** They are structurally identical (Id + Name);
  five near-identical controllers would be copy-paste. The `{kind}` segment is parsed against
  an enum, so an unknown value gives a clean `UNKNOWN_LOOKUP` 400, never a 404 mystery.
- **Sorting is whitelisted** to a fixed set of columns — the client sends a column name, never
  an expression, so there is no injection surface.
- **Deleting is always soft** (`IsEnabled = 0`), and a lookup still referenced by an
  installation refuses to delete rather than leaving blank names in the grid.
- TypeScript client generation from `[EndpointName]` was **not** wired into the build; the
  frontend uses a small hand-written typed client instead (see `6_frontend_installations_ui.md`).
  The `[EndpointName]` attributes are all in place, so generation can be added later without
  touching the controllers.
