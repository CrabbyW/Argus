# Plan: EntityJournal — historie změn jedné instalace

**Date:** 2026-08-04  
**Status:** `Completed`  
**Description:** Nová databázová tabulka `EntityJournal`: kdy, kdo, jak a co změnil, na úrovni
jedné instalace. Doposud nesla instalace jen `CreatedUtc` a `ModifiedUtc`, takže po dvou týdnech
nikdo neřekl, že se přestěhovala z ALBERTY na GAIIS1 a kdo to udělal. Souborový akční log z plánu
13 na to neodpovídá — zaznamenává odeslaný požadavek, ne rozdíl proti předchozímu stavu, a maže se
retencí. Součástí je čtecí endpoint a záložka **History** v detailu instalace.

---

## Checklist

### 1. Tabulka

- [x] `EntityJournalEntry` → tabulka `EntityJournal`; `EntityJournalEntryConfiguration`
- [x] Sloupce: `ChangeSetId`, `InstallationId` (FK, Cascade), `EntityName`, `Action`, `Field`,
      `OldValue`/`NewValue`, `OldValueId`/`NewValueId`, `ChangedBy`, `ChangedUtc`
- [x] Index `IX_EntityJournal_Installation` nad `(InstallationId, ChangedUtc)`
- [x] Žádný `IsEnabled`, žádný `HasQueryFilter`, žádná retence
- [x] Migrace `20260804083401_AddEntityJournal` — vytváří jen novou tabulku a index

### 2. Zápis

- [x] `EntityJournalInterceptor` (`ISaveChangesInterceptor`) čte změny z `ChangeTracker`
- [x] Skalární diff instalace přes `OriginalValues` vs `CurrentValues`, `Field` v lidských názvech
      (`Machine`, ne `MachineId`) podle mapy `JournalFields`
- [x] `IsEnabled` true→false = `Deleted`, ne řádek o sloupci; `CreatedUtc`/`ModifiedUtc` ignorovány
- [x] Přidání/odebrání `InstallationTags` a `InstallationRepositories` = `LinkAdded`/`LinkRemoved`
      klíčované na instalaci — pokrývá i editaci z obrazovky Repositories
- [x] Založení: řádek `Created` se dopisuje v `SavedChanges`, protože Id vzniká až při insertu
- [x] `ValueResolver` překládá FK na jméno **v okamžiku zápisu**, nikdy neselže (`#id` jako fallback)
- [x] `ICurrentUserAccessor` + `AddHttpContextAccessor()`; bez přihlášení `system`
- [x] `ArgusDbContext.JournalingSuppressed`; `DbSeeder` ho zapíná

### 3. Čtení

- [x] `IEntityJournalService` / `EntityJournalService` — jen čtení, nejnovější první, strop 500
- [x] `POST /api/installations/{id}/journal`, `[EndpointName("Installations_ReadInstallationJournal")]`
- [x] 404 na neexistující instalaci; historie soft-smazané instalace je čitelná

### 4. Frontend

- [x] `JournalEntry` v `types.ts`, `api.getInstallationJournal`
- [x] `InstallationHistory.tsx` — tabulka When | Who | Action | Field | From | To nad `useSheetStyles`
- [x] Záložky Details / History v `InstallationDetailDrawer`, drawer rozšířen na 660px
- [x] Historie se načítá až při přepnutí na záložku; po změně řádku se vrací na Details

### 5. Ověření

- [x] `dotnet build` a `pnpm build` bez varování, `pnpm lint` bez nálezů
- [x] `EntityJournalTests` — 12 nových případů, celkem 112 testů prochází
- [x] Proti běžícímu API: seedovaná instalace má prázdnou historii, jedna editace zapsala tři
      řádky s jedním `ChangeSetId` (`Machine ALBERTA → GAIIS1`, `Valid to`, `LinkAdded Tag`),
      založení `Created`, smazání `Deleted`, neznámé Id 404, obrazovka `/logs` beze změny

---

## Notes

- **Interceptor, ne volání ve službách.** Change tracker je jediné místo, které ví, jaká hodnota
  *byla*, a `UpdateInstallationAsync` už entitu načítá trackovaně. Druhý důvod je pokrytí:
  `AppRepositoryService` mění `InstallationRepositories` z druhé strany, takže ručně psaný žurnál
  v `InstallationService` by o každém propojení z obrazovky Repositories mlčel.
- **Výjimka z konvence „controller předá jméno".** Žurnál se zachytává pod službami, takže by to
  znamenalo nový parametr na všech zápisových metodách dvou služeb a přepsání konstruktorů, které
  používá skoro každý test. Proto `ICurrentUserAccessor` nad `IHttpContextAccessor` — a jen pro
  něj; pro zápisy na úrovni služeb platí konvence dál.
- **Hodnoty se překládají při zápisu, ne při čtení.** Přejmenování stroje v číselníku nesmí
  přepsat, co se stalo minulý měsíc. Hlídá to test
  `Renaming_a_lookup_afterwards_does_not_change_the_history`.
- **Seeder nežurnáluje.** 200 demo instalací by vyrobilo dvě stě řádků, které nikdo neudělal, a
  první skutečná změna by v nich zanikla. Seedovaná instalace má prázdnou historii — což je pravda.
- **Uložení beze změny nezapíše nic.** Nejčastější akce uživatele je otevřít dialog, kouknout a dát
  Uložit; kdyby to psalo řádky, byla by historie do týdne převážně šum.
- **Založení je jeden řádek, ne dvanáct.** Per-field rozpis vzniku instalace je šum — celý stav
  nese sama instalace. Ze stejného důvodu se nezapisují vazby, se kterými instalace vznikla.
- **Žádná retence, záměrně.** Auditní tabulka, které nejstarší řádky samy mizí, není audit. Řádek
  má pár set bajtů; kdyby tabulka jednou opravdu narostla, správná odpověď je vědomá archivace
  operátorem, ne background služba mažící důkazy. Napsáno i v XML komentáři entity.
- Mimo rozsah (rozhodnuto v zadání): žurnál číselníků a uživatelů, a role admin/user — až vzniknou,
  patří History mezi pohledy, které se za ně schovají.
