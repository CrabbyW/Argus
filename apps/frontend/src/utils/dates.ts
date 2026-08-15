/**
 * Datumy se v aplikaci píšou česky: `15. 8. 2026`, čas `15. 8. 2026 21:51`.
 *
 * Locale je zadaná napevno (`cs-CZ`), ne podle prohlížeče. Argus je vnitřní nástroj jednoho
 * provozu a záznam, který se čte na dvou strojích, musí vypadat stejně — jinak by jeden člověk
 * viděl `8/15/2026` a druhý `15. 8. 2026` a při telefonátu by si nerozuměli.
 *
 * Data z API chodí dvojí: `validFromDate` je samotné datum (`2026-08-15`, bez pásma) a
 * `createdUtc` je okamžik v UTC. To první se nesmí hnát přes `new Date()` — ISO datum se parsuje
 * jako půlnoc UTC a v pásmu za Greenwichem by se posunulo o den zpátky.
 */
const DATE_FORMAT = new Intl.DateTimeFormat('cs-CZ', {
  day: 'numeric',
  month: 'numeric',
  year: 'numeric',
});

const DATE_TIME_FORMAT = new Intl.DateTimeFormat('cs-CZ', {
  day: 'numeric',
  month: 'numeric',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** `2026-08-15` → `15. 8. 2026`. Bez převodu pásma — je to datum, ne okamžik. */
export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);

  if (!match) {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? value : DATE_FORMAT.format(parsed);
  }

  const [, year, month, day] = match;
  return DATE_FORMAT.format(new Date(Number(year), Number(month) - 1, Number(day)));
}

/** Okamžik v UTC → místní čas česky, `15. 8. 2026 21:51`. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  // Serverové časy chodí bez `Z`; bez ní by je prohlížeč četl jako místní a hodiny by seděly
  // jen v zimě na jednom serveru.
  const normalised = /[Zz]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`;
  const parsed = new Date(normalised);

  return Number.isNaN(parsed.getTime()) ? value : DATE_TIME_FORMAT.format(parsed);
}
