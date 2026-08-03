# Argus — značka

Koncept 2: monogram **A** složený z dlaždic (každá dlaždice = jedna instalace). Fluent styl,
plochý tvar, měkké rohy.

## Soubory

| Soubor | Použití |
| --- | --- |
| `argus-logo.svg` | Plné logo (značka + název + claim) na světlém pozadí |
| `argus-logo-dark.svg` | Plné logo na tmavém pozadí |
| `argus-mark.svg` | Samotná značka, barevná — hlavička aplikace, dokumenty |
| `argus-mark-mono.svg` | Jednobarevná značka, dědí `currentColor` — inline v UI, tisk |
| `favicon.svg` | Zjednodušená verze bez tenkých patek, pro 16–48 px |
| `argus-app-icon.svg` | 512×512 dlaždice s bílou značkou — PWA / instalační ikona |
| `logo-koncepty.html` | Původní náhled všech čtyř konceptů (archiv) |

## Barvy

| Role | Hex |
| --- | --- |
| Brand primary | `#0F6CBD` |
| Accent (prostřední dlaždice) | `#479EF5` |
| Tmavá / wordmark | `#0C3B5E` |
| Mono / text | `#242424` |
| Claim, sekundární text | `#616161` |

Odpovídá Fluent UI `brand` rampě (`colorBrandBackground` = `#0F6CBD`).

## Pravidla

- **Ochranná zóna:** minimálně výška jedné dlaždice (12 j. v 64 viewBoxu) na všech stranách.
- **Minimální velikost:** plné logo 120 px šířky; samotná značka 24 px; pod 24 px použijte `favicon.svg`.
- Neměňte proporce, nedoplňujte stíny ani gradienty, neotáčejte.
- Na tmavém pozadí použijte bílou variantu, ne barevnou.

## Poznámky k písmu

Wordmark v `argus-logo*.svg` používá živý text (Segoe UI Variable / Segoe UI). Na Windows se
vykreslí správně; pro externí použití (web bez Segoe, tisk) je vhodné text převést na křivky.

## Rastrové exporty

PNG (favicon 16/32/48, PWA 192/512) zatím nejsou vygenerované — sandbox pro spuštění konverze
nebyl dostupný. Lze doplnit z SVG kdykoli.
