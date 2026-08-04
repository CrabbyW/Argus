# Plan: User management — a Users screen over `ApplicationUsers`

**Date:** 2026-08-03
**Status:** `Complete` — 2026-08-03, verified against the running LocalDB (§4)
**Description:** `ApplicationUsers` has been a table since Phase 5 and the only way to get a row
into it is `DbSeeder`, which seeds `msfadmin` once and then never runs again. Argus therefore has
exactly one account for its whole life, and its password can only be changed by deleting the row
by hand in SSMS. This plan gives users the same treatment every other table already has: a list
screen, create/edit, soft delete, and — the part no other table needs — setting a password.

---

## 0. Why users are not a lookup

The lookup layer (`Services/Lookups/`) is a good fit for anything shaped `Id | Name |
Description | IsEnabled`, and it is tempting to register users as an eleventh kind. That would be
wrong on three counts and the temptation is worth naming so nobody re-litigates it later:

- `LookupUpsertDto` carries a name and a description. A user needs a **password**, which must
  never travel in the same payload shape that `GET` returns — the generic screen does a
  read-modify-`PUT`, so a password field there would be round-tripped through the browser.
- `Usage` — the in-use check every lookup gets — asks "does an installation reference this Id?".
  No installation references a user. The guards a user needs are different in kind: *do not
  disable yourself*, *do not disable the last account that can still log in*.
- Reading users is not like reading a lookup. A lookup is world-readable to anyone signed in; the
  user list is the one place where the answer is "who can get in", and it deserves its own
  endpoint with its own log line.

So: `IUserService` + `UsersController`, following the same conventions as the rest
(`ApiResponse<T>`, `[EndpointName]`, lowercase log4net `logger`, `AsNoTracking()` on reads).

## 1. The contract

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/users?includeDisabled=` | List. Enabled only by default, same as every other screen |
| `GET` | `/api/users/{id}` | One user |
| `POST` | `/api/users` | Create — username, display name, password |
| `PUT` | `/api/users/{id}` | Edit username + display name. **Never the password** |
| `POST` | `/api/users/{id}/password` | Set a password |
| `DELETE` | `/api/users/{id}` | Soft delete (`IsEnabled = 0`) |
| `POST` | `/api/users/{id}/restore` | Undo a soft delete |

No DTO ever carries `PasswordHash` or `PasswordSalt`. `UserDto` is
`Id, Username, DisplayName, IsEnabled, CreatedUtc, LastLoginUtc`.

`restore` exists because `ApplicationUser` has `HasQueryFilter(x => x.IsEnabled)`: without it a
disabled account is invisible to the API and could never be brought back through the UI. The
lookups live with that; an account someone still needs should not have to be repaired in SSMS.

### The two guards

1. **You cannot disable or delete yourself.** The request comes with your own token; acting on
   your own row logs you out mid-session with no obvious cause.
2. **You cannot disable the last enabled user.** This is the one that actually matters: without
   it, two clicks lock every human out of Argus permanently, and `DbSeeder` will not help —
   it only seeds when the table is *empty*, and a soft-deleted row is not empty.

Both return 400 with a message that says which rule was hit.

### Password rules

Minimum 8 characters, checked in the service rather than only by `[StringLength]` on the DTO, so
the rule holds no matter which caller arrives. Hashing goes through the existing
`PasswordHasher` (PBKDF2-SHA256, 210k iterations, per-user salt) — unchanged, and no plaintext is
logged anywhere.

Changing your *own* password does not require the old one. That is a deliberate simplification
for a single-tenant internal tool where every signed-in session is already trusted with every
other user's password; it is noted here rather than assumed.

## 2. Seeding, and the `msfadmin` password

`DbSeeder.SeedUsersAsync` returns early when any user exists, so `Seed:AdminPassword` has had no
effect since the database was first created. That behaviour stays — a seeder that rewrites a
password on every boot would silently undo a change made through the new screen. What changes is
that the escape hatch is no longer SSMS: the Users screen can set it.

The dev config already carries `msfadmin` / `msfadmin`, and the live row already matched it — the
2026-07-31 rebuild seeded from that config — so nothing needed resetting. Confirmed by signing in;
see §4.

## 3. Checklist

### Backend
- [x] `WebApiPoco/Users/UserDtos.cs` — `UserDto`, `UserUpsertDto`, `SetPasswordDto`
- [x] `Services/IUserService.cs` + `UserService.cs` with both guards
- [x] `Controllers/UsersController.cs` — seven actions, `[Authorize]`, `[ProducesResponseType]`
- [x] Registered in `Program.cs`
- [x] No DTO exposes hash or salt _(checked by reading the projection, and by a test)_

### Tests
- [x] Create hashes the password and the user can then log in
- [x] Password is never returned by any endpoint
- [x] Duplicate username is rejected (400, not a unique-index 500)
- [x] Short password is rejected
- [x] Disabling yourself is rejected
- [x] Disabling the last enabled user is rejected
- [x] Disabled user cannot log in; restoring lets them in again
- [x] Edit does not touch the password hash

### Frontend
- [x] `api/types.ts` — `User`, `UserUpsert`, `SetPassword`
- [x] `api/client.ts` — the seven calls
- [x] `pages/UsersPage.tsx` — grid, create/edit dialog, set-password dialog, disable/restore
- [x] Route `/users` + nav entry
- [x] Show-disabled toggle in the URL, like every other piece of grid state

### Documentation
- [x] README: the address table, and the pre-deployment note that the password is now changeable
      in the app
- [x] `progress.txt` entry

### Gates
- [x] `dotnet build` 0 errors 0 warnings
- [x] `pnpm run test`
- [x] `pnpm run build`, `pnpm run lint`
- [x] `msfadmin` password actually set to `msfadmin` against the running LocalDB, verified by
      logging in

---

## Notes

- **The password is `msfadmin`, at the user's explicit request.** It is a local demo credential
  on a LocalDB instance that only accepts the Windows account that owns it. The README's
  pre-deployment checklist already says to change it, and now says where.
- Roles and permissions are **out of scope**. Every user who can sign in can do everything,
  which is what Argus has always done. Adding a role column later does not disturb this shape.

---

## 4. Verification — 2026-08-03

`dotnet build` 0 errors 0 warnings · `pnpm run test` **90** green (79 + 11 new) · `pnpm run build`
and `pnpm run lint` clean.

Exercised over HTTP against the running API and the real LocalDB, signed in as `msfadmin`:

| Check | Result |
|---|---|
| `msfadmin` / `msfadmin` signs in | token issued |
| `GET /api/users` | one account, no hash or salt in the payload |
| `DELETE /api/users/1` (self) | 400 — *"You cannot disable your own account"* |
| Create `tester` | 201, and `tester` could then sign in with the password given |
| Create `MSFADMIN` | 400 — *"Username 'MSFADMIN' is already taken"*, not a unique-index 500 |
| Password `abc` | 400 — *"must be at least 8 characters"* |
| `POST /users/2/password` | 200; the old password stopped working, the new one worked |
| `DELETE /api/users/2` | 200; `tester` could no longer sign in |
| `GET /api/users` vs `?includeDisabled=true` | `[msfadmin]` vs `[msfadmin, tester(disabled)]` |

**The last-enabled-user guard could not be reached over HTTP**, and that is the correct behaviour
rather than a gap: the only way to be the last enabled user here is to be `msfadmin`, and acting on
your own row hits the self-guard first. The guard itself is covered by
`The_last_enabled_user_cannot_be_disabled`, which acts as somebody else.

The `tester` row was removed from the database afterwards, so the demo data is back to the single
seeded account.

### The password

`msfadmin` / `msfadmin`, confirmed by signing in. It was already what `appsettings.Development.json`
carried — the 2026-07-31 rebuild seeded from that config — so nothing had to be reset. What changed
is that it is now changeable from the app instead of only in SSMS.

---

## 5. Visual check — 2026-08-03

Screenshotted headless at 1600×1000, signed in, in **both themes** (`prefers-color-scheme`
emulated for the light one). The screen reads as the rest of the app: ruled sheet, filled header
band, Id column narrow and right-aligned, `Active` / `Disabled` as tinted badges, and the row for
the signed-in account carries a `you` badge with its disable button greyed out — the self-guard
visible before it is hit rather than only as a 400 afterwards.

The full-width table with one flexible column (`Display name` here, `Description` on Lookups) is
the house style, not an oversight — the two screens were compared side by side.

**Harness note.** The CDP harness had to be rebuilt, and Node's built-in `WebSocket` cannot drive
Chrome DevTools: it negotiates `permessage-deflate`, which the DevTools server does not honour, so
frames are sent and nothing ever comes back — it hangs rather than erroring. The scratchpad script
uses a hand-rolled client that offers no extensions. Worth knowing before anyone burns an hour on
it again.
