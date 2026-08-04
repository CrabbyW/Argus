# Plan: Admin Panel Refinements — Lookup Values, Action Log, Detail Drawer

**Date:** 2026-08-03  
**Status:** `Completed`  
**Description:** Tři úpravy admin panelu. (1) Hlavní mřížka instalací přestává zobrazovat
Id a ukazuje výhradně dohledané hodnoty z číselníků. (2) Backend zapisuje každou akci do
vlastního log souboru ve formátu `[timestamp] [action] [command] [status]`, včetně
konkrétního odeslaného příkazu, a staré logy se automaticky mažou po konfigurovatelném
počtu dní (default 30). (3) Detail vybrané položky se otevírá v side draweru zprava místo
modálního dialogu a filtrovací tlačítko `Clear` se mění na `Show all`.

---

## Checklist

### 1. Main view — pryč s Id, zobrazit lookup hodnoty

- [x] Zrušit `ID_COLUMNS` a přepínač `Show names / Show Ids` včetně `view` parametru v URL
- [x] Odstranit sloupec `ID` a pomocnou funkci `idCell`
- [x] Ponechat jen jmenné sloupce (mřížka vždy zobrazuje dohledané hodnoty)

### 2. Logging systém

- [x] `AuditLogOptions` — sekce `AuditLog` v konfiguraci (`RetentionDays`, `Directory`)
- [x] `ActionAuditLoggingMiddleware` — jeden řádek na každý API požadavek
- [x] Zdrojem `action` je `EndpointName` z atributu na akci controlleru
- [x] `command` = skutečně odeslaný požadavek (metoda, cesta, query, tělo) s redakcí hesel
- [x] Samostatný log4net appender `AuditFile` → `logs/argus-actions.log`
- [x] Registrace middleware v `Program.cs`

### 3. Retence logů

- [x] `LogRetentionService` (BackgroundService) mazající soubory starší než X dní
- [x] `AuditLog:RetentionDays` v `appsettings.Example.json` i `appsettings.Development.json`, default 30
- [x] Běh při startu a poté jednou za 24 hodin

### 4. Drawer + Show all

- [x] `InstallationDetailDrawer` (Fluent `OverlayDrawer`, `position="end"`) nahrazuje `InstallationDetailDialog`
- [x] Kliknutí na řádek mřížky otevře drawer; vybraný řádek je zvýrazněný
- [x] Smazat `InstallationDetailDialog.tsx`
- [x] Tlačítko `Clear` přejmenovat na `Show all`

### 5. Ověření

- [x] `dotnet build` backendu prochází
- [x] `pnpm nx run argus-frontend:build` prochází
- [x] Backendové testy prochází

---

## Notes

- **Interpretace "konkrétního odeslaného příkazu":** Argus nemá typed command jako
  auto-type nástroj; ekvivalentem skutečně odeslaného příkazu je HTTP požadavek. Do pole
  `command` jde tedy `METODA /cesta?query {tělo}` — tedy to, co klient reálně poslal, ne
  jen název události. Hesla a tokeny v těle se nahrazují `***`.
- Retenci nelze pokrýt log4net `maxSizeRollBackups` — to je počet souborů, ne dnů. Proto
  samostatná background služba mazající podle `LastWriteTimeUtc`.
- Drawer je zatím jen nad mřížkou instalací (hlavní obrazovka). Číselníky a uživatelé
  zůstávají na dialozích — nebylo v zadání.
- Aesthetic: žádné nové barvy, vše z Fluent tokenů, drawer bez vlastních animací navíc.
