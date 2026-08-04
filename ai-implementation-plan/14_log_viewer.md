# Plan: Log Viewer — obrazovka pro čtení logů

**Date:** 2026-08-04  
**Status:** `Completed`  
**Description:** Logy, které API od plánu 13 zapisuje (akční log `argus-actions.log`
a diagnostický `argus-api.log`), šlo dosud číst jen přes vzdálenou plochu a textový editor
na serveru. Nová obrazovka `/logs` je zpřístupňuje přímo v aplikaci: výběr souboru, filtr
řádků, počet posledních řádků a volitelné automatické obnovování. Výhradně pro čtení.

---

## Checklist

### 1. Backend — služba nad soubory

- [x] `ILogFileService` / `LogFileService` — výpis souborů a čtení konce souboru
- [x] Nabízí jen soubory odpovídající `AuditLog:FilePatterns` ze stejné konfigurace, kterou
      používá `LogRetentionService`
- [x] Název souboru z požadavku se nikdy neskládá s adresářem — porovná se s už povoleným
      výpisem, takže `../appsettings.json` prostě neodpovídá ničemu (404)
- [x] Čtení proudem přes kruhový buffer posledních N řádků; strop 5000 řádků na odpověď
- [x] `FileShare.ReadWrite`, aby šel číst i soubor, který log4net právě drží otevřený
- [x] Filtr `searchTerm` (case-insensitive substring) se aplikuje při průchodu souborem

### 2. Backend — endpointy

- [x] `POST /api/logs/search` → seznam souborů (`name`, `kind`, `sizeBytes`, `lastWriteUtc`)
- [x] `POST /api/logs/{name}/read` → konec souboru (`lines`, `totalLines`, `isTruncated`)
- [x] `[Authorize]` jako všude jinde; čtení přes POST podle konvence `ReadRequestDto`
- [x] Registrace `ILogFileService` v `Program.cs` (singleton — stav nemá)

### 3. Frontend — obrazovka `/logs`

- [x] `LogsPage.tsx` — výběr souboru, pole `Contains`, počet řádků, `Auto-refresh`, `Refresh`
- [x] Stav (`file`, `q`, `lines`) v URL, takže pohled jde poslat kolegovi
- [x] Monospace panel bez zalamování, ERROR/5xx červeně, WARN/4xx oranžově
- [x] Záložka `Logs` a routa v `App.tsx`; `api.getLogFiles` / `api.getLogContent` v klientovi

### 4. Ověření

- [x] `dotnet build` a `pnpm build` (tsc + vite) prochází, `pnpm lint` bez nálezů
- [x] `LogFileServiceTests` — výpis, tail, filtr, otevřený soubor, čtyři varianty traversalu
- [x] Testy: 100/100 prochází
- [x] Ruční průchod proti běžícímu API: výpis 6 souborů, tail akčního logu, filtr,
      traversal → 404, nepřihlášený požadavek → 401

---

## Notes

- **Jen pro čtení, záměrně až do controlleru.** Není tu nic, co maže nebo rotuje soubor.
  Auditní stopa, kterou umí smazat její vlastní obrazovka, není auditní stopa; mazání
  zůstává na `LogRetentionService` a `AuditLog:RetentionDays`.
- **Proč strop 5000 řádků.** Log je jediná věc v Argusu, která reálně naroste do gigabajtů.
  Klient posílá `maxLines`, server ho ořízne — nelze si vyžádat celý soubor.
- `kind` (`action` / `diagnostic`) posílá server, aby UI nemuselo rozpoznávat názvy souborů.
- Obrazovka je zatím dostupná každému přihlášenému uživateli. Argus nemá role; až vzniknou,
  patří `/logs` mezi první, co se za ně schová.
