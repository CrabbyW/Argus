# Plan: SQL do diagnostického logu — most z Microsoft.Extensions.Logging do log4net

**Date:** 2026-08-08  
**Status:** `Completed`  
**Description:** Příkazy, které EF Core posílá do databáze, nebyly vidět nikde než v terminálu
vývojáře. Důvod: log4net se konfiguroval sám pro sebe (`XmlConfigurator.Configure`), zatímco
framework — EF Core, hosting, autentizace — píše přes `Microsoft.Extensions.Logging`, o té
konfiguraci neví, a tak jeho řádky končily jen u výchozího konzolového provideru. Do
`logs/argus-api.log` se tedy nedostaly a obrazovka `/logs` z plánu 14 je neuměla ukázat.
Balíček `Microsoft.Extensions.Logging.Log4Net.AspNetCore` už v projektu byl referencovaný, jen
nezapojený; tenhle plán ho zapojuje.

---

## Checklist

### 1. Zapojení mostu

- [x] `builder.Logging.ClearProviders()` — výchozí konzolový provider by tiskl každý řádek
      podruhé vedle log4netího `ConsoleAppender`
- [x] `builder.Logging.AddLog4Net(new Log4NetProviderOptions { ExternalConfigurationSetup = true })`
- [x] `ExternalConfigurationSetup = true`, protože log4net konfiguruje o dva řádky výš
      `XmlConfigurator`; jinak provider přečte `log4net.config` znovu a appendery se navěsí dvakrát
- [x] Bez nového `using` — `Log4NetProviderOptions` i `AddLog4Net` jsou v namespace
      `Microsoft.Extensions.Logging`, který je mezi implicitními usings Web SDK
- [x] Bez zásahu do `Argus.Api.csproj` — `PackageReference` na provider už tam byl

### 2. Kolik toho má přitéct

- [x] `Microsoft.EntityFrameworkCore.Database.Command` = `Information` v `appsettings.Example.json`
      — jeden záznam na každý příkaz odeslaný do databáze
- [x] `Microsoft.EntityFrameworkCore.Infrastructure` = `Warning` (jinak hlásí každé postavení
      kontextu), `Migrations` = `Information` (běží při startu, je o čem vědět)
- [x] Filtruje se na straně `Logging:LogLevel`, ne v `log4net.config` — jedno místo, ne dvě
- [x] Komentovaný blok `//Logging` v příkladu konfigurace vysvětluje, co ta kategorie je a jak ji
      vypnout

### 3. Dokumentace

- [x] Komentář v `Program.cs` — proč most existuje a proč obě jeho volby
- [x] Komentář nad appenderem `RollingFile` v `log4net.config` — co v tom souboru nově je a kde se
      to reguluje
- [x] `README.md`, tabulka obrazovek: u `/logs` zmíněno, že diagnostický log nese i SQL

### 4. Ověření

- [ ] `dotnet build` a testy _(v tomto prostředí není .NET SDK — nespuštěno, viz Notes)_
- [ ] Proti běžícímu API: otevřít `/logs`, vybrat `argus-api.log`, filtr `Executed DbCommand`
      _(vyžaduje běžící API — nespuštěno)_

---

## Notes

- **Akční log tímhle nezměkl.** `ArgusAudit` má `additivity="false"` a vlastní appender, takže do
  `argus-actions.log` nic z frameworku nepřiteče. Zůstává, čím byl: jeden řádek na požadavek, čtyři
  hranaté závorky, strojově čitelné.
- **Hodnoty parametrů se neloguují.** EF je píše jako `'?'`, dokud někdo nezavolá
  `EnableSensitiveDataLogging()`. To se tu nevolá a volat nemá: `argus-api.log` je soubor, který
  aplikace sama vystavuje přes API na obrazovku `/logs` — hesla a osobní data v něm nemají co dělat.
  Napsáno i v komentáři u konfigurace, protože tohle je přesně nastavení, které někdo jednou zapne
  „jen na chvíli při ladění".
- **Objem.** Na seedovaném startu (migrace + 200 instalací) je SQL zdaleka nejobjemnější položka
  logu. Retence z plánu 13 na to platí beze změny (`AuditLog:RetentionDays`, `maximumFileSize`
  10 MB, 14 záloh), ale kdyby byl provoz větší, správná odpověď je přepnout `Database.Command` na
  `Warning` — proto je ta kategorie v příkladu vypsaná zvlášť, ne schovaná pod `Default`.
- **Proč ne `LogTo` na `DbContextu`.** Cílenější, ale řeší jen EF. Most řeší i hosting, JWT a
  cokoli dalšího, co framework hlásí — a to je pro diagnostický log to, co v něm dosud chybělo
  úplně stejně jako SQL.
- **Konvence `CLAUDE-dotnet.md` platí dál.** Aplikační kód si logger pořád bere staticky
  (`private static readonly ILog logger`), pořád žádné `Console.Write`. Most je jen o cizím kódu,
  který si logger vzít nemůže.
- **Nepřeloženo/nespuštěno.** Prostředí téhle relace nemá .NET SDK (`dotnet` neexistuje), takže
  build ani testy neproběhly. Rizikový bod by byl podpis `AddLog4Net` — ověřen čtením metadat
  balíčku `Microsoft.Extensions.Logging.Log4Net.AspNetCore` 10.0.0 (`lib/net10.0`): typ
  `Microsoft.Extensions.Logging.Log4NetProviderOptions` s vlastností `ExternalConfigurationSetup`
  a přetížení `AddLog4Net(builder, options)` v něm skutečně jsou.
