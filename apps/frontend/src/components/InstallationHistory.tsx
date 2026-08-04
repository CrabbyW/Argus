import { useEffect, useState } from 'react';
import {
  Badge,
  MessageBar,
  MessageBarBody,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { api } from '../api/client';
import type { JournalEntry } from '../api/types';
import { useSheetStyles } from '../styles/sheetStyles';

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '12px' },
  muted: { color: tokens.colorNeutralForeground3 },
  wrapper: { overflowX: 'auto' },

  // Fluent lays a table out with fixed columns, so without explicit widths the timestamp ran
  // straight over the username instead of widening its own column. The minimum is what the six
  // columns need before a date starts wrapping mid-value; under it the wrapper scrolls sideways.
  table: { minWidth: '690px' },
  when: { whiteSpace: 'nowrap' },
  value: { wordBreak: 'break-word' },
  // The first row of each save keeps a hairline above it, so an edit that changed four fields
  // reads as one action rather than four unrelated ones.
  changeSetStart: { borderTop: `2px solid ${tokens.colorNeutralStroke1}` },

  // The value that is going away. Muted, not red: on an ordinary edit the old value is history,
  // not a fault, and a column of red dates reads as a screen full of errors. Red is kept for the
  // one case that really is a removal.
  previous: { color: tokens.colorNeutralForeground3 },
  removed: { color: tokens.colorPaletteRedForeground1 },
});

/** Local time reads better than UTC; the value on the wire stays UTC. */
function formatUtc(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
}

/** The badge colour carries the same meaning as the word, for scanning down the column. */
function actionColor(action: JournalEntry['action']) {
  switch (action) {
    case 'Created':
    case 'LinkAdded':
      return 'success' as const;
    case 'Deleted':
    case 'LinkRemoved':
      return 'danger' as const;
    default:
      return 'informative' as const;
  }
}

/**
 * The change history of one installation, straight from the `EntityJournal` table.
 *
 * Deliberately not the same thing as the `/logs` screen: that one shows the requests the API
 * received, this one shows what a row looked like before somebody changed it. The values here are
 * the names as they were at the time — renaming a machine later does not rewrite this table.
 *
 * The history only goes back to the day the journal was deployed; installations seeded or edited
 * before that show nothing, and the empty state says so rather than implying nobody touched them.
 */
export function InstallationHistory({ installationId }: { installationId: number }) {
  const styles = useStyles();
  const sheet = useSheetStyles();

  const [entries, setEntries] = useState<JournalEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    setEntries(null);
    setError(null);

    api
      .getInstallationJournal(installationId)
      .then((result) => {
        if (!cancelled) setEntries(result);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load the history.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [installationId]);

  if (error) {
    return (
      <MessageBar intent="error">
        <MessageBarBody>{error}</MessageBarBody>
      </MessageBar>
    );
  }

  if (!entries) {
    return <Spinner label="Loading history..." />;
  }

  if (entries.length === 0) {
    return (
      <Text className={styles.muted}>
        No recorded changes. The journal records edits made from this application; anything older
        than it is not in here.
      </Text>
    );
  }

  const dash = <span className={styles.muted}>—</span>;

  return (
    <div className={styles.root}>
      <Text className={styles.muted}>
        {entries.length} change{entries.length === 1 ? '' : 's'}, newest first
      </Text>

      <div className={styles.wrapper}>
        {/* mergeClasses, not template strings: Griffel's atomic classes have to be merged by it
            or the last one silently wins. */}
        <Table size="small" className={mergeClasses(sheet.table, styles.table)}>
          <colgroup>
            <col style={{ width: '150px' }} />
            <col style={{ width: '100px' }} />
            <col style={{ width: '105px' }} />
            <col style={{ width: '115px' }} />
            <col />
            <col />
          </colgroup>

          <TableHeader>
            <TableRow>
              <TableHeaderCell className={sheet.headerCell}>When</TableHeaderCell>
              <TableHeaderCell className={sheet.headerCell}>Who</TableHeaderCell>
              <TableHeaderCell className={sheet.headerCell}>Action</TableHeaderCell>
              <TableHeaderCell className={sheet.headerCell}>Field</TableHeaderCell>
              <TableHeaderCell className={sheet.headerCell}>From</TableHeaderCell>
              <TableHeaderCell className={sheet.headerCell}>To</TableHeaderCell>
            </TableRow>
          </TableHeader>

          <TableBody>
            {entries.map((entry, index) => {
              // One save wrote several rows: repeating the timestamp and the username on each of
              // them would read as several separate edits.
              const startsChangeSet =
                index === 0 || entries[index - 1].changeSetId !== entry.changeSetId;

              return (
                <TableRow key={entry.id} className={startsChangeSet ? styles.changeSetStart : undefined}>
                  <TableCell className={styles.when}>
                    {startsChangeSet ? formatUtc(entry.changedUtc) : ''}
                  </TableCell>
                  <TableCell>{startsChangeSet ? entry.changedBy : ''}</TableCell>
                  <TableCell>
                    <Badge appearance="tint" color={actionColor(entry.action)} size="small">
                      {entry.action}
                    </Badge>
                  </TableCell>
                  <TableCell>{entry.field ?? dash}</TableCell>
                  <TableCell
                    className={mergeClasses(
                      styles.value,
                      entry.action === 'LinkRemoved' ? styles.removed : styles.previous,
                    )}
                  >
                    {entry.oldValue ?? dash}
                  </TableCell>
                  <TableCell className={styles.value}>{entry.newValue ?? dash}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
