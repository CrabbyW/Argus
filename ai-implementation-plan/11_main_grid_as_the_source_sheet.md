# Plan: The main grid is the ApplicationInstalation sheet

**Date:** 2026-08-01
**Status:** `Complete` — 2026-08-01, verified against screenshots of the running app (§5)
**Description:** The roadplan's central rule — every shared value gets an Id, and the main table
holds *only those numbers* — is implemented in the database and invisible everywhere else. The
grid shows resolved names and the list contract does not even carry the foreign keys. This plan
makes the main screen show what the source workbook shows.

---

## 0. The requirement, quoted rather than paraphrased

`roadplan:51-59`:

> Proto: každý sdílený údaj je vlastní číselník s Id, a instalace na něj jen odkazuje.
>
>     Machines [Id, MachineName]
>     27 ... GAIIS1
>     28 ... GAIIS2
>
>     ApplicationInstalation [Id, MachineId, AppNameId, AppStageNameId, ...]
>     1 / 27 / 5 / 2 / ...

`docs/reference/zdrojova-tabulka-4-applicationinstalation.png` shows the same thing and nothing
else: header `ID | MachineId | AppNameId | AppStageNameId`, one data row `1 | 4 | 3`. The lookup
sheets are `ID | Name`. **The fact table is numbers. The lookups are where names live.**

This was read as a database requirement and satisfied there — `ApplicationInstallations` is
foreign keys, `InstallationUpsertDto` accepts Ids only. It was never carried into the UI, which
is the only part of Argus the user actually sees. Three sessions of UI work went past it.

## 1. Ground truth — measured, not assumed

Screenshot of the running app at 1600×1000, authenticated (`scratchpad/grid-before.png`):

- **No foreign key is displayed anywhere in the grid.** Machine reads `GAIIS1`, not `2`.
- **`InstallationListItemDto` carries no foreign keys at all** — only `Id` and resolved name
  strings. The frontend cannot show the numbers because it is never sent them. This is a backend
  change, not just a column change.
- **Four columns truncate at the default width**: `ProAssist CallCent…`, `Data Exchange W…`,
  `https://vipsprava.1…`, and Application generally. Names are wide; Ids are not.
- **The app follows the OS theme and this machine is dark**, so `colorNeutralStroke2` gridlines
  sit at very low contrast against `colorNeutralBackground1`. The sheet ruling added earlier is
  nearly invisible — it reads as a dark list, not as a workbook.

## 2. What changes

### 2.1 Backend — send the numbers

- `InstallationListItemDto` gains `MachineId`, `AppNameId`, `AppStageNameId`,
  `ProcessorArchitectureId`, `DnsEndpointId` (nullable), `RootPathId`, `PhysicalPathId`
  (nullable). The resolved names stay: the Id view needs them for tooltips, and the name view
  needs them outright.
- `InstallationMapper` fills them from the entity's existing foreign keys — no new query, the
  values are already loaded.
- A test pins that a list row reports the same foreign keys the entity holds. Without it the
  mapper can silently drop one and the grid shows a blank column.

### 2.2 Frontend — the grid *is* the sheet

Default view, column for column as the source sheet:

    ID | MachineId | AppNameId | AppStageNameId | ProcessorArchitectureId
       | DnsEndpointId | RootPathId | PhysicalPathId | ValidFrom | ValidTo | IsActive

- Numbers right-aligned, tabular figures, monospace — a column of Ids should line up.
- Nullable references (`DnsEndpointId`, `PhysicalPathId`) render as an empty cell, not `—`: an
  empty cell is what a spreadsheet shows for "no value", and the roadplan calls both optional.
- **Every Id cell carries its resolved name as a `title`**, so hovering answers "what is 4?"
  without leaving the row. The lookup screens remain the place where the mapping lives.
- Tags and repositories are M:N and have no single Id to show; they stay as they are, after the
  scalar columns, matching the roadplan's `Tags M:N přes InstallationTag` line.

**Names view.** A `Ids ⇄ Names` toggle in the page header swaps the reference columns for the
resolved names. Rationale, stated plainly because it is an addition to the brief: the roadplan's
own success criteria are questions in words — *which machines serve paha.ga.local?* — and an
answer of `2` is not an answer. The Id view is the default because that is what the sheet shows;
the name view exists so the screen can still answer the questions the project is justified by.
The toggle lives in the URL (`?view=names`) like every other piece of grid state.

### 2.3 Frontend — make it read as a workbook

- **Gridlines must be visible in both themes.** `colorNeutralStroke2` is too faint on dark;
  use `colorNeutralStroke1`, which is the token meant for a visible border.
- **Header band** gets a bottom rule of `colorNeutralStroke1` and stays filled.
- **Column widths follow content**: Id columns are narrow (~90px) and stop truncating names,
  because in the default view there are no names to truncate. The name view keeps the wider
  widths it needs.
- Row-number gutter stays — it is the sheet's own row numbering and it already works.

## 3. Checklist

- [x] `InstallationListItemDto` carries the seven foreign keys
- [x] `InstallationMapper` fills them
- [x] Test: list row foreign keys match the entity
      _(plus a second one pinning that the two optional references stay null rather than 0)_
- [x] `InstallationListItem` (TS) mirrors the DTO exactly
- [x] Grid renders Ids by default, in the source sheet's column order
- [x] Each Id cell has its resolved name on hover
- [x] `?view=names` toggle, default Ids
- [x] Gridlines visible on dark and light _(`colorNeutralStroke2` → `colorNeutralStroke1`)_
- [x] No column truncates in either view
- [x] `dotnet build` 0/0, `pnpm run test` **79**, `pnpm run build`, `pnpm run lint` all clean
- [x] Screenshot both views and compare against the source sheet

## 4. Out of scope

The database model, which already satisfies the rule. The filter bar rebuilt earlier this
session stays as it is.

---

## 5. Verification — 2026-08-01

Driven headless over CDP against the running stack, authenticated, at 1600×1000.

**Id view** (`scratchpad/grid-ids.png`) renders
`ID | MachineId | AppNameId | AppStageNameId | ProcessorArchitectureId | DnsEndpointId |
RootPathId | PhysicalPathId` — numbers, right-aligned, monospaced. Row 3 (the Data Exchange
worker) shows an empty `DnsEndpointId`, which is the nullable-reference case rendering as a blank
cell rather than a `0` or a dash.

**Names view** (`grid-names.png`) resolves all of them with nothing truncated and both `Active`
and `Actions` on screen — the widths in `NAME_COLUMNS` were measured against the real seed values
rather than guessed.

**Lookups** (`lookups.png`) reads as the workbook's `Machines` sheet: a narrow `Id` column then
`Name`. It needed a `colgroup` — Fluent's fixed layout had been giving `Id` a full quarter of the
table, wider than the names it numbers.

### One correction to the record

A deep link to `/lookups/machines` appeared to be rewritten to `/installations`, and this was
briefly reported as a routing bug. **It was not.** It was an artefact of the screenshot harness,
which navigated via `/login` — an unconditional `<Navigate to="/installations">` at `App.tsx:129`
— so that redirect landed after the harness's own navigation and overrode it. Probing the app
directly, signed out, `/lookups/machines?x=1` keeps both path and query exactly as `App.tsx:84-86`
intends. The harness was fixed to seed the token in place and reload instead.
