# Plan: Security Hardening — Auth Lifecycle, Brute Force, Transport, RBAC

**Date:** 2026-07-30
**Status:** `In Progress`
**Description:** A security review of the Argus API and frontend found no injection or
cryptographic defects — every query is parameterized EF Core LINQ, sorting is a hard-coded
whitelist, and password hashing meets current OWASP guidance. What it did find is a set of gaps
around the **session lifecycle and the deployment posture**: a login endpoint that can be
hammered without limit, access tokens that survive their user being disabled, no transport
security, and no distinction between a reader and an administrator. This plan closes them in
that order — the first two are the ones that turn a single stolen or guessed credential into
eight hours of unstoppable full access.

Nothing in this plan changes the data model's meaning or the shape of any existing response
payload, with one deliberate exception: `Jwt:TokenLifetimeMinutes` drops from 480 to 60, which
users will notice as more frequent logins until section B ships a refresh flow.

---

## Checklist

### A. Brute force and login abuse — `HIGH`

- [ ] Register the built-in rate limiter in `Program.cs`
      (`builder.Services.AddRateLimiter(...)`, `app.UseRateLimiter()` placed before
      `UseAuthentication`)
- [ ] Add a named policy `"login"` — fixed window partitioned on the client IP
      (`HttpContext.Connection.RemoteIpAddress`), 10 attempts per 5 minutes, `QueueLimit = 0`,
      rejection status `429`
- [ ] Add a global fallback policy for the rest of the API (partitioned on IP, generous —
      e.g. 300 requests/minute) so one client cannot saturate the pool
- [ ] Apply `[EnableRateLimiting("login")]` to `AuthController.Login`
- [ ] Make the `429` response body a standard `ErrorResponse`
      (`ErrorCode = "TOO_MANY_ATTEMPTS"`) via `RateLimiterOptions.OnRejected`, not an empty body
- [ ] Add per-account lockout on `ApplicationUser`: `FailedLoginCount` (int) and
      `LockedOutUntilUtc` (DateTime?), a migration for both
- [ ] `AuthService.LoginAsync`: refuse a locked-out account **before** running PBKDF2, increment
      the counter on failure, reset it on success, lock for 15 minutes after 10 consecutive
      failures
- [ ] The lockout response must stay indistinguishable from a wrong password — same
      `INVALID_CREDENTIALS` code, same message, same timing path. Do not tell an attacker they
      found a real account.
- [ ] Frontend: `LoginPage` renders the `429` message instead of the generic failure banner

**Why this is first.** `POST /api/auth/login` is anonymous and uncounted, and every attempt
costs the server 210,000 PBKDF2 iterations (~100 ms of CPU) while costing the attacker a single
HTTP request. That is both an unlimited online password-guessing oracle *and* a CPU-exhaustion
amplifier: a few dozen concurrent requests will starve the thread pool and take the API down for
everyone. The IP-partitioned limiter fixes the DoS, the per-account lockout fixes the guessing —
neither substitutes for the other, because an attacker with a botnet defeats the first and an
attacker spraying one password across many accounts defeats the second.

### B. Tokens outlive the account — `HIGH`

- [ ] Add `SecurityStamp` (string, non-null) to `ApplicationUser` + migration; populate existing
      rows with a fresh GUID in the migration's `Up`
- [ ] `AuthService.CreateToken` emits the current stamp as a private claim (`"sst"`)
- [ ] Regenerate the stamp whenever the account changes state — disabled, password changed,
      forced sign-out
- [ ] Wire `JwtBearerEvents.OnTokenValidated` in `Program.cs`: load the user by the `sub` claim,
      `context.Fail()` unless the user is found (the global `IsEnabled` filter handles disabled
      accounts implicitly) **and** the token's `sst` claim matches the stored stamp
- [ ] Keep that lookup cheap — `AsNoTracking()`, projecting `Id`/`SecurityStamp` only. Add
      `IMemoryCache` with a 30-second entry only if a measurement shows it is needed; do not
      pre-optimize a single indexed read.
- [ ] Drop `Jwt:TokenLifetimeMinutes` from 480 to 60 in `appsettings.Example.json` and
      `JwtOptions`
- [ ] Add a refresh-token flow so the shorter lifetime is not a usability regression:
      `RefreshToken` entity (hash, user, expiry, revoked-at, replaced-by),
      `POST /api/auth/refresh`, rotation on every use, reuse-detection revokes the whole chain
- [ ] `POST /api/auth/logout` revokes the presented refresh token
- [ ] Frontend `client.ts`: on a `401`, attempt a single silent refresh before clearing the
      session; serialize concurrent refreshes so a burst of parallel requests triggers one call
- [ ] Tests: a disabled user's still-unexpired token is rejected on `/api/installations`;
      a rotated refresh token cannot be replayed

**Why.** `ApplicationUserConfiguration.cs:23` puts a global query filter on `IsEnabled`, so a
disabled user genuinely cannot log in and `GET /api/auth/me` genuinely rejects them. But
`InstallationsController`, `LookupsController` and `AppRepositoriesController` only check the JWT
*signature* — they never touch the database. Disabling a compromised account therefore does
nothing to the token already in the attacker's hands: full read/write CRUD continues for up to
eight hours, and there is no revocation list, no stamp, and no refresh flow to shorten the
window. "Disable the account" has to actually mean something.

### C. Transport and deployment posture — `MEDIUM`

- [ ] `app.UseHttpsRedirection()` and `app.UseHsts()` in `Program.cs`, both skipped in
      Development so the LocalDB/Vite loop keeps working on `http://localhost:5080`
- [ ] Replace `"AllowedHosts": "*"` in `appsettings.Example.json` with the real host name(s), and
      document that the wildcard is a Development-only value
- [ ] Default `Database:MigrateAndSeedOnStartup` to `false` — flip the `GetValue` fallback in
      `Program.cs:123` from `true` to `false` and set it explicitly to `true` in the example
      Development config only
- [ ] Rename the seeded demo account away from `msfadmin` (use `argusadmin`), and log a WARN at
      startup whenever seeding actually runs
- [ ] Document that the production connection string should hold a least-privilege login with no
      DDL rights, and that migrations are applied by `pnpm run db:migrate` in the deploy step,
      not by the running application
- [ ] Security response headers, added as a small middleware ahead of everything else:
      `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`,
      `X-Frame-Options: DENY`, and a `Content-Security-Policy` of
      `default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'`
- [ ] Verify the CSP against the built frontend — Fluent UI injects styles at runtime, so
      `style-src` will need `'unsafe-inline'` unless a nonce is threaded through; confirm which,
      do not guess
- [ ] `.env.example`: remove the working default password, leave the key with an empty value; in
      `docker-compose.yml` drop the `:-Argus_Dev_Pwd1!` fallback so the stack refuses to start
      without an explicit password, and bind the port to `127.0.0.1:1433:1433`

**Why.** As configured the API speaks plain HTTP, so the login POST body and every subsequent
bearer token cross the network in cleartext. `Database:MigrateAndSeedOnStartup` defaulting to
`true` means a production deployment that forgets to override it will apply DDL under whatever
rights the app's connection string has and create a privileged account named `msfadmin` — the
default account from Metasploitable, present in every credential-stuffing wordlist in existence.
The committed `Argus_Dev_Pwd1!` fallback is dev-only, but it stands up SQL Server on `0.0.0.0:1433`
with a password that is in the public repository.

### D. Everyone is an administrator — `MEDIUM`

- [ ] Add `Role` to `ApplicationUser` (enum `Reader` | `Editor` | `Admin`, default `Reader`) +
      migration; set the seeded demo account to `Admin`
- [ ] Emit the role as a `ClaimTypes.Role` claim in `AuthService.CreateToken`
- [ ] Register three policies in `Program.cs`: `CanRead` (any authenticated), `CanEdit`
      (`Editor`/`Admin`), `CanAdminister` (`Admin`)
- [ ] `CanRead` on every GET; `CanEdit` on installation and repository POST/PUT/DELETE;
      `CanAdminister` on all of `LookupsController`'s write actions — a lookup row is shared by
      every installation that references it, so deleting one is not an ordinary edit
- [ ] Return the role in `CurrentUserDto` and hide write affordances in the UI for a `Reader`
      (hiding the button is cosmetic; the policy is the control)
- [ ] Tests: a `Reader` token gets `403` on `DELETE /api/installations/{id}`

**Why.** There is no authorization beyond "is authenticated" anywhere in the codebase. Any user
who can log in can delete any installation, any machine, any DNS endpoint, and any repository.
Plan 7 recorded this as deliberate ("Roles are not in `roadplan`"), which was reasonable for the
demo — but Argus is positioned as a system of record whose value is that its history survives, and
right now every account can erase that history. This is the point at which it should stop being
out of scope.

### E. Smaller items — `LOW`

- [ ] Sanitize user-controlled values before they reach a log line — strip CR/LF and truncate.
      `AuthService.cs:35` interpolates the raw submitted username, which may contain newlines and
      can therefore forge entries in `logs/argus-api.log`; `AppRepositoryService.cs:61,86` and
      `LookupService.cs:94,154` have the same shape with URLs and names. A single
      `LogSanitizer.Clean(string)` helper covers all four.
- [ ] Record the source IP on the failed-login WARN. Without it the line cannot support a
      brute-force investigation, which is most of what it exists for.
- [ ] Scope `IgnoreQueryFilters()` in `InstallationService.cs:37` to the `Installation` entity
      alone. It is query-wide, so `IncludeDisabled=true` also unhides soft-deleted machines,
      applications, DNS endpoints and repositories through the `Include`s. Use the EF Core 10
      per-navigation form, or apply the filter as an explicit `Where` on `Installations` and keep
      the global filters intact.
- [ ] Cap `DataViewFilterBase.SearchTerm` at 200 characters in the setter, mirroring how
      `PageSize` clamps. It feeds six `LIKE '%…%'` predicates, so an unbounded term is a
      free full-table scan.
- [ ] Validate `RepositoryUrl`'s scheme in `AppRepositoryService` against an allowlist
      (`http`, `https`, `git`, `ssh`, `svn`, `bitbucket`). Nothing renders it as a link today —
      `RepositoriesPage.tsx:248` and `InstallationDetailDialog.tsx:142` both print it as text — so
      there is no live XSS, but `javascript:alert(1)` is storable right now and the day someone
      makes that cell clickable it becomes one.
- [ ] Move the token out of `localStorage` (`client.ts:34-38`) once section B lands: keep the
      access token in a module-scoped variable and the refresh token in an `HttpOnly; Secure;
      SameSite=Strict` cookie, so a script on the origin cannot read either.

---

## Verification

- [ ] `dotnet build` clean (0 warnings, 0 errors) and `pnpm run build` clean
- [ ] `dotnet test` — existing suites still pass, plus the new cases in B and D
- [ ] Manual: 11 rapid bad logins return `429`, and the 11th does **not** reveal whether the
      account exists
- [ ] Manual: log in, disable the user directly in SQL, confirm the next
      `GET /api/installations` returns `401` rather than data
- [ ] Manual: a `Reader` account can list installations and cannot delete one
- [ ] Confirm the CSP does not break Fluent UI in a real browser — build, serve `dist/`, check
      the console for violations

---

## Notes

- **No code has been written yet.** This plan is the deliverable of the security review; the
  checklist is the implementation brief. Check items off as they land, per
  `CLAUDE-planning-standards.MD`.
- **Order matters.** A and B are independent of each other and both are independent of C, D
  and E — but D's role claim and B's security-stamp claim both touch `CreateToken`, so doing B
  before D avoids editing the same method twice. E's last item explicitly depends on B.
- **What the review found to be sound**, and should not be "improved" by accident:
  - Every database access is EF Core LINQ. There is no `FromSqlRaw`, no `ExecuteSqlRaw`, no
    string-concatenated SQL anywhere in the codebase.
  - `PasswordHasher` is PBKDF2-SHA256 at 210,000 iterations with a 16-byte per-user salt and
    `CryptographicOperations.FixedTimeEquals`. That meets OWASP's 2023 guidance; leave it alone.
  - `InstallationService.ApplySort` is a hard-coded `switch`, not interpolation into `OrderBy` —
    the usual sorting injection hole is already closed.
  - Login failure is uniform across unknown user, disabled user and wrong password, so usernames
    cannot be enumerated. Section A must preserve this property, including for lockout.
  - `GlobalExceptionHandlerMiddleware` returns a trace id and never a stack trace, and correctly
    declines to rewrite a response that has already started.
  - Swagger is registered only under `IsDevelopment()`. Startup refuses to boot without a
    32-character signing key and without `Seed:AdminPassword`, with no fallback literal in source
    for either.
  - The upsert DTOs accept lookup **Ids** only, never names, so there is no mass-assignment path
    that could invent a machine or application.
- **The frontend has no HTML-injection sink today** — no `dangerouslySetInnerHTML`, no
  `innerHTML`, no `eval`, and React escapes everything these pages render. That is why the
  `localStorage` token is ranked `LOW` rather than `HIGH`: the theft primitive does not currently
  exist. It is still worth fixing, because the impact if one ever appears is total and
  irreversible for eight hours (section B shortens that to one).
- **`ClockSkew` is set to 1 minute** rather than the 5-minute default, which is good and should
  survive the token-lifetime change.
- Section C's HSTS header is only meaningful once Argus is actually served over TLS behind its
  real host name. Adding it while the deployment is still plain HTTP is harmless but does
  nothing — the transport change is the substance, the header is the follow-through.
