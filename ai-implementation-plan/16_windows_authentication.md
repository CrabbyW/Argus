# Plan: Windows autentizace + přihlašování v logu

**Date:** 2026-09-01  
**Status:** `Completed`  
**Description:** Druhá cesta do Argusu vedle jména a hesla: tlačítko **Sign in with Windows** pod
přihlašovacím formulářem, které použije doménový účet z prohlížeče (Negotiate). Ať se uživatel
dostane dovnitř kterýmkoli způsobem, dostane stejný JWT — zbytek API o druhé cestě neví. Druhá
polovina zadání je log: akční log dosud u přihlášení říkal jen „POST /api/auth/login vrátil 200"
(heslo maskované, jinak nic). Nově se u **každého** požadavku loguje kdo a jak je přihlášený a u
přihlášení samotného i doménový účet, IP, user agent a důvod odmítnutí.

Poznámka k `CLAUDE-dotnet.md`: šablona z TrafficEventu předepisuje Negotiate na každém controlleru.
Tady je Negotiate schválně jen na jednom endpointu — viz Notes.

---

## Checklist

### 1. Databáze

- [x] `ApplicationUser.WindowsAccountName` (nullable, 256) — doménový účet ve tvaru `DOMAIN\user`
- [x] `ApplicationUser.LastLoginMethod` (nullable, 16) — `Password` / `Windows`
- [x] `PasswordHash` a `PasswordSalt` nullable: účet přihlašovaný jen přes Windows heslo nemá
- [x] Filtrovaný unikátní index `IX_ApplicationUsers_WindowsAccountName`
      (`WHERE [WindowsAccountName] IS NOT NULL`) — jeden doménový účet = jeden uživatel Argusu
- [x] Migrace `20260901090000_AddWindowsAuthentication` (+ Designer, snapshot)

### 2. Konfigurace

- [x] `WindowsAuthOptions` — sekce `WindowsAuth`, `Enabled` a `AutoProvisionUsers`, obojí `false`
- [x] `appsettings.Example.json`: sekce i komentář, proč je to vypnuté

### 3. Backend

- [x] `AddNegotiate()` v `Program.cs` vedle JWT; výchozí schéma zůstává JWT
- [x] `POST /api/auth/windows-login` — jediný endpoint s `[Authorize(NegotiateDefaults...)]`,
      vrací stejné `LoginResponseDto` jako heslový formulář
- [x] `GET /api/auth/options` (anonymní) — jestli má přihlašovací obrazovka kreslit tlačítko
- [x] `POST /api/auth/logout` — nic neruší, existuje kvůli logu
- [x] `AuthService.WindowsLoginAsync` — mapování účtu case-insensitive, zakázaný uživatel odmítnut,
      `AutoProvisionUsers` volitelně založí uživatele (`CORP\jnovak` → username `jnovak`)
- [x] Claim `authMethod` (a `windowsAccount`) v tokenu; `AuthenticationMethod` v odpovědích
- [x] Heslový formulář odmítne účet bez hesla — „nemá heslo" nikdy není přijaté heslo
- [x] `UserService`: mapování se zakládá i edituje, kontrola duplicity, heslo povinné jen když
      uživatel nemá doménový účet, a mapování nejde odebrat uživateli bez hesla

### 4. Log

- [x] `ILoginAuditLog` / `LoginAuditLog` — `Auth_LoginSucceeded`, `Auth_LoginFailed`,
      `Auth_SignedOut` do akčního logu, formát
      `[action] [method=…; username=…; windowsAccount=…; ip=…; userAgent=…; reason=…] [status] [actor]`
- [x] Neúspěch jde i do diagnostického logu (`WARN`) — klient dál dostává vágní hlášku
- [x] `ActionAuditLoggingMiddleware`: pátý sloupec `[actor]` = `jnovak (Windows)` / `anonymous`
- [x] Vstupy z požadavku se čistí (bez `\n`, `[`, `]`, strop 256 znaků) — jeden záznam = jeden řádek

### 5. Frontend

- [x] `api.getAuthOptions`, `api.loginWithWindows` (`credentials: 'include'`), `api.logout`
- [x] `AuthContext.loginWithWindows`; `logout` to nejdřív oznámí serveru
- [x] `ModernLogin`: oddělovač „or" a druhé, obtažené tlačítko — jen když ho stránka dostane
- [x] `LoginPage` se ptá `/api/auth/options`; při chybě prostě tlačítko nekreslí
- [x] `UsersPage`: sloupec **Windows account**, u posledního přihlášení odznak `Password`/`Windows`,
      pole pro mapování v dialogu (i při editaci, na rozdíl od hesla)

### 6. Ověření

- [x] `AuthServiceTests`: mapovaný účet dostane token s `authMethod=Windows`, shoda bez ohledu na
      velikost písmen, nemapovaný účet odmítnut, auto-provisioning založí uživatele, kolize
      jména auto-provisioning odmítne, zakázaný uživatel neprojde ani přes Windows, účet bez hesla
      neprojde formulářem, heslové přihlášení se zapíše jako `Password`
- [x] `tsc -b` a `eslint` na frontendu čisté
- [ ] Proti běžícímu API na doméně — nelze ověřit v tomto prostředí (Linux, bez SQL Serveru
      a bez domény); Negotiate handshake ověřit při nasazení, viz Notes

---

## Notes

- **Negotiate jen na jednom endpointu.** Handshake stojí kolo navíc a musí ho podpořit prohlížeč i
  hostitel; kdyby visel na každém controlleru, platila by se ta cena při každém požadavku a API by
  mělo dvě různé podoby identity. Takhle je Windows jen *způsob, jak získat token* — všechno
  ostatní vidí pořád jen bearer JWT. Proti šabloně `CLAUDE-dotnet.md` je to úmyslná odchylka, ve
  stejném duchu jako poznámka v `progress.txt` o tom, že Argus Negotiate neměl vůbec.
- **Handler se registruje vždy, přepínač je v controlleru.** Schéma endpointu se váže při startu,
  `WindowsAuth:Enabled` je nastavení. Registrace handleru sama o sobě nic nestojí, dokud se proti
  ní nikdo neautentizuje, a s vypnutým přepínačem endpoint odmítne a obrazovka tlačítko nekreslí.
- **Auto-provisioning je vypnutý.** Na doméně by jinak byl uživatelem Argusu každý, kdo se dostane
  na URL. Kde je členství v doméně zároveň autorizace, se zapne; jinak se účty mapují ručně na
  obrazovce Users. Kolize krátkého jména s existujícím účtem se **nikdy** neslije do jednoho
  uživatele — to by bylo nejhorší možné uhodnutí.
- **U Windows přihlášení se neodpovídá vágně.** Volající už prokázal, který doménový účet je;
  „váš účet není namapovaný" je rozdíl mezi žádostí u správce a nekonečným opakováním. Vágní
  zůstává heslový formulář, kde jde o existenci uživatelského jména.
- **Pátý sloupec v akčním logu.** Se dvěma cestami dovnitř samotné jméno neříká, na základě čeho
  byl požadavek považovaný za důvěryhodný. Čtečky, které soubor dělí po hranatých závorkách,
  dostanou pole navíc na konci řádku, takže dosavadní čtyři sloupce zůstávají na svých místech.
- **Odhlášení nic neruší.** Token se jen zahodí v prohlížeči; endpoint existuje, aby po odhlášení
  zůstala stopa. Selhat proto smí bez následku — nikoho to nesmí udržet přihlášeného.
- **Vite proxy vs. Negotiate.** Ve vývoji jde `/api` přes proxy Vitu a NTLM/Kerberos je vázané na
  spojení; pokud tudy handshake neprojde, testuje se tlačítko proti API napřímo (`Cors:AllowedOrigins`
  + `AllowCredentials` už to umí). V nasazení, kde frontend i API stojí za jedním hostem, problém
  není.
- **`Down` migrace maže uživatele bez hesla.** Sloupce se nedají vrátit na `NOT NULL`, dokud
  existují; přihlásit se na starém schématu by stejně nemohli. Je to napsané v migraci u toho SQL.
- Mimo rozsah: role a oprávnění (kdo smí na Users), skupiny z AD a odvozování oprávnění z nich,
  automatické přihlášení bez kliknutí na tlačítko.
