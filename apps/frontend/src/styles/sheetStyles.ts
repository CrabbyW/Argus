import { makeStyles, tokens } from '@fluentui/react-components';

/**
 * The spreadsheet look, shared by every grid in the app.
 *
 * Argus's data model was derived from a workbook — `docs/reference/zdrojova-tabulka-*.png` — and
 * the people who will use it read that workbook today. Fluent's default table underlines each
 * row and leaves columns unbounded, which reads as a web list; a sheet rules every cell, fills
 * the header band, and numbers the rows down a narrow gutter. Matching that costs nothing and
 * makes the screens legible to someone arriving from the workbook.
 *
 * Colours come from Fluent tokens rather than Excel's literal palette so the theme still
 * switches. Only the structure is borrowed.
 */
export const useSheetStyles = makeStyles({
  // Ruled on all four sides. `borderCollapse` matters: without it the doubled borders between
  // adjacent cells read as a thick grey seam instead of a single line.
  //
  // Stroke1, not Stroke2. Stroke2 is a subtle divider and on the dark theme it sits so close to
  // the row background that the ruling disappears and the grid reads as a plain list — which is
  // exactly what it looked like before this was measured against a screenshot.
  table: {
    borderCollapse: 'collapse',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    '& td, & th': {
      borderRight: `1px solid ${tokens.colorNeutralStroke1}`,
      borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    '& tr:hover td': { backgroundColor: tokens.colorNeutralBackground1Hover },
  },

  // The header band: filled, so it separates from the page rather than floating on it, and closed
  // off underneath the way a frozen header row is.
  headerCell: {
    backgroundColor: tokens.colorNeutralBackground3,
    borderBottom: `2px solid ${tokens.colorNeutralStroke1}`,
    fontWeight: tokens.fontWeightSemibold,
    whiteSpace: 'nowrap',
  },

  // The row-number gutter and the Id column: same fill as the header, numbers right-aligned and
  // tabular so digits line up down the column the way a sheet sets them.
  gutterCell: {
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground3,
    textAlign: 'right',
    fontVariantNumeric: 'tabular-nums',
    fontSize: tokens.fontSizeBase200,
    userSelect: 'none',
  },

  // Id values inside the body. Right-aligned, tabular and monospaced so a column of references
  // lines up digit under digit — full foreground colour, because in the Id view these numbers
  // are the data, not a subtitle to it.
  idCell: {
    textAlign: 'right',
    fontFamily: tokens.fontFamilyMonospace,
    fontVariantNumeric: 'tabular-nums',
    color: tokens.colorNeutralForeground1,
  },
});
