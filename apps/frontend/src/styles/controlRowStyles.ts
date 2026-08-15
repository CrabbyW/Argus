import { makeStyles, tokens } from '@fluentui/react-components';

/**
 * The horizontal rows of controls — filter bars, the lookup editor — shared so they line up with
 * each other and so the alignment rule is written once.
 *
 * The rule is: align on the input line, not on the bottom edge. The children of these rows are
 * different heights (a `Field` with a hint or a validation message is a line taller than one
 * without, and a switch or a button has no label above it at all), so `align-items: end` lines up
 * the outer boxes and leaves the controls themselves stepping down the row like stairs. Aligned
 * from the top, every label sits on one line and every input on the next; the hints hang off the
 * bottom, where nothing else is measured against them.
 */
export const useControlRowStyles = makeStyles({
  row: {
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
    alignItems: 'flex-start',
  },

  /**
   * Drops a label-less control — a switch, a lone button — onto the same line as the labelled
   * inputs beside it: one line of label text, plus the three `spacingVerticalXXS` gaps Fluent's
   * vertical `Field` puts above, below and after its label. Both values are the tokens `Field`
   * itself uses, so the offset tracks the theme rather than a measured pixel count.
   */
  labelledRow: {
    marginTop: `calc(${tokens.lineHeightBase300} + 3 * ${tokens.spacingVerticalXXS})`,
  },

  /** The same offset for a group of buttons, kept together at the end of a row. */
  buttonRow: {
    display: 'flex',
    gap: '8px',
    marginTop: `calc(${tokens.lineHeightBase300} + 3 * ${tokens.spacingVerticalXXS})`,
  },
});
