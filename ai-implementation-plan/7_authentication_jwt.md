# Plan: Authentication — ApplicationUser + JWT (Phase 5)

**Date:** 2026-07-29
**Status:** `Complete`
**Description:** Classic username + password login against the `ApplicationUser` table, issuing
a JWT that protects every API endpoint, with a login screen on the frontend.

---

## Checklist

- [x] `ApplicationUser` entity: username, display name, password hash, salt, IsEnabled, timestamps
- [x] `PasswordHasher` — PBKDF2-SHA256, 210k iterations, per-user salt, constant-time compare
- [x] `JwtOptions` bound from configuration (`Jwt` section)
- [x] `AuthService` — credential check, `LastLoginUtc` update, token issuance
- [x] `AuthController` — `POST /api/auth/login` (anonymous), `GET /api/auth/me` (protected)
- [x] JWT bearer authentication registered in `Program.cs` with full validation
- [x] All non-auth controllers carry `[Authorize(AuthenticationSchemes = JwtBearer)]`
- [x] Startup refuses to boot if `Jwt:SigningKey` is missing or under 32 characters
- [x] Frontend `AuthProvider` + `LoginPage`; 401 clears the token and returns to login
- [x] Demo user seeded (`admin`), password from `Seed:AdminPassword` configuration

---

## Notes

- **Passwords are never stored or logged in plaintext.** Failed logins log the *username only*,
  at WARN, never the attempted password.
- **Login failures are deliberately vague** ("Invalid username or password") and take the same
  path whether the user is unknown, disabled, or the password is wrong — it must not be
  possible to enumerate valid usernames.
- **Token lifetime is 8 hours** (`TokenLifetimeMinutes: 480`) with a 1-minute clock skew.
  No refresh-token flow — out of scope for the demo; the user logs in again.
- **`CLAUDE-dotnet.md` mentions RSA PEM keys shared between two backends.** Argus has one
  backend, so a symmetric HMAC-SHA256 key is used instead. The rule it exists to serve — keys
  live in configuration/`secrets/` and never in source — is followed: `appsettings*.json` and
  `secrets/` are gitignored, and the app will not start without a key supplied externally.
- **No registration endpoint.** `roadplan` describes login only; users are created by seeding
  or directly in the database. Adding self-registration would be inventing scope.
- Authorization is currently authentication-only — any signed-in user may edit anything.
  Roles are not in `roadplan`; if they arrive, they attach to `ApplicationUser` as claims.
