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
/**
 * The width of the leftmost numeric column — the Id on the lookup and user sheets, the row-number
 * gutter on the installations grid. One constant because these screens are read side by side and
 * a gutter that changes width from tab to tab makes the tables look misaligned with each other.
 * Wide enough for five digits in the monospaced face `idCell` sets.
 */
export const ID_COLUMN_WIDTH = '70px';

/**
 * A `<col>` width is a *ratio*, not a size, once the table is wider than its columns.
 *
 * With `table-layout: fixed` and `width: 100%`, the leftover space is shared out over every
 * column that names a width — so a 70px Id column becomes 78px on one sheet and 91px on the next,
 * depending on what else that sheet's columns add up to. Sizing it in `px` therefore does not make
 * it the same width everywhere; it only makes it the same *before* the surplus is handed out.
 *
 * Every grid so gives exactly one column no width at all. An unsized column takes the whole
 * surplus and the sized ones — the Id gutter first among them — keep the pixels they asked for.
 * Pick the column with the longest values for the job (a path, a description); that is the one
 * that wants the extra room anyway.
 */

/**
 * The row-actions column, likewise one constant for every sheet. Each grid ends in the same strip
 * of subtle icon buttons, and a strip that is 110px on one tab and 140px on the next makes the
 * right-hand edge of the app move as you switch between them. Wide enough for four small buttons
 * plus the cell padding — the installations grid has the most.
 */
export const ACTIONS_COLUMN_WIDTH = '140px';

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
  /**
   * A text cell on a sheet holds one line.
   *
   * `table-layout: fixed` plus wrapping is what makes a grid look broken: a name a few characters
   * too long for its column breaks into two lines, that row grows taller than the rest, and the
   * ruling stops reading as a sheet. The columns below are sized so real values fit; anything
   * genuinely longer is clipped with an ellipsis and kept in full in the cell's `title`, which is
   * how a spreadsheet behaves too.
   */
  textCell: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },

  idCell: {
    textAlign: 'right',
    fontFamily: tokens.fontFamilyMonospace,
    fontVariantNumeric: 'tabular-nums',
    color: tokens.colorNeutralForeground1,
  },
});
