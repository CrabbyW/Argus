import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  MessageBar,
  MessageBarBody,
  OverlayDrawer,
  Spinner,
  Tab,
  TabList,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { DismissRegular, EditRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { InstallationDetail } from '../api/types';
import { InstallationHistory } from './InstallationHistory';
import { formatDate, formatDateTime } from '../utils/dates';

const useStyles = makeStyles({
  // Wide enough for a physical path on one line, narrow enough to leave the grid readable
  // behind it — the point of a drawer over a dialog is that the row stays in view. 460px was
  // that balance for the detail alone; the History tab is a six-column table and needs the rest.
  surface: { width: '740px', maxWidth: '100vw' },

  tabs: { marginBottom: '16px' },

  grid: {
    display: 'grid',
    gridTemplateColumns: 'minmax(120px, auto) 1fr',
    columnGap: '16px',
    rowGap: '10px',
    alignItems: 'baseline',
  },
  label: { color: tokens.colorNeutralForeground3 },
  value: { wordBreak: 'break-word' },
  mono: {
    fontFamily: tokens.fontFamilyMonospace,
    wordBreak: 'break-all',
  },
  repos: { margin: 0, paddingLeft: '18px' },
  tagList: { display: 'flex', flexWrap: 'wrap', gap: '4px' },
  // The drawer has no DialogActions band, so the actions sit at the end of the body,
  // separated by the same hairline the rest of the app uses.
  actions: {
    display: 'flex',
    gap: '8px',
    marginTop: '24px',
    paddingTop: '16px',
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

interface Props {
  installationId: number;
  onClose: () => void;
  onEdit: () => void;
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  const styles = useStyles();

  return (
    <>
      <Text className={styles.label}>{label}</Text>
      <div className={styles.value}>{children}</div>
    </>
  );
}

/**
 * The detail of the selected row, as a panel sliding in from the right rather than a modal.
 *
 * Reading is what this screen is for — the roadplan's questions are answered by scanning the
 * grid and then checking one row. A dialog covers the grid and drops the reader's place in
 * it; a drawer leaves the list where it was, so "and the next one?" costs one click.
 */
export function InstallationDetailDrawer({ installationId, onClose, onEdit }: Props) {
  const styles = useStyles();

  const [detail, setDetail] = useState<InstallationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tab, setTab] = useState<'details' | 'history'>('details');

  useEffect(() => {
    let cancelled = false;

    // Back to the detail when the selection moves: the history of the previous row is not an
    // answer to a question about this one.
    setTab('details');

    // Reset first: the drawer stays mounted while the selection moves from row to row, and
    // without this the previous installation would show under the new one's title.
    setDetail(null);
    setError(null);

    api
      .getInstallation(installationId)
      .then((result) => {
        if (!cancelled) setDetail(result);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load the installation.');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [installationId]);

  const dash = <span className={styles.label}>—</span>;

  return (
    <OverlayDrawer
      open
      position="end"
      className={styles.surface}
      onOpenChange={(_, data) => !data.open && onClose()}
    >
      <DrawerHeader>
        <DrawerHeaderTitle
          action={
            <Button
              appearance="subtle"
              aria-label="Close"
              icon={<DismissRegular />}
              onClick={onClose}
            />
          }
        >
          {detail ? `${detail.appName} on ${detail.machineName}` : 'Installation'}
        </DrawerHeaderTitle>
      </DrawerHeader>

      <DrawerBody>
        {error && (
          <MessageBar intent="error">
            <MessageBarBody>{error}</MessageBarBody>
          </MessageBar>
        )}

        {!detail && !error && <Spinner label="Loading..." />}

        {detail && (
          <>
            <TabList
              className={styles.tabs}
              selectedValue={tab}
              onTabSelect={(_, data) => setTab(data.value as 'details' | 'history')}
            >
              <Tab value="details">Details</Tab>
              <Tab value="history">History</Tab>
            </TabList>

            {/* Mounted only when selected, so the journal is fetched on demand rather than
                behind every row the user clicks through. */}
            {tab === 'history' && <InstallationHistory installationId={installationId} />}
          </>
        )}

        {detail && tab === 'details' && (
          <>
            <div className={styles.grid}>
              <Row label="Machine">{detail.machineName}</Row>
              <Row label="Application">{detail.appName}</Row>
              <Row label="Stage">{detail.appStageName}</Row>
              <Row label="Architecture">{detail.processorArchitecture}</Row>
              <Row label="DNS">{detail.dnsName ?? dash}</Row>
              <Row label="Root path">
                <span className={styles.mono}>{detail.rootPath}</span>
              </Row>
              <Row label="Physical path">
                {detail.physicalPath ? (
                  <span className={styles.mono}>{detail.physicalPath}</span>
                ) : (
                  dash
                )}
              </Row>
              <Row label="Tags">
                {detail.tags.length === 0 ? (
                  dash
                ) : (
                  <div className={styles.tagList}>
                    {detail.tags.map((tag) => (
                      <Badge key={tag.id} appearance="tint" color="informative">
                        {tag.name}
                      </Badge>
                    ))}
                  </div>
                )}
              </Row>
              <Row label="Valid">
                {formatDate(detail.validFromDate)} →{' '}
                {detail.validToDate ? formatDate(detail.validToDate) : 'open'}
              </Row>
              <Row label="Status">
                <Badge appearance="filled" color={detail.isActive ? 'success' : 'informative'}>
                  {detail.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </Row>
              <Row label="Created">{formatDateTime(detail.createdUtc)}</Row>
              <Row label="Modified">
                {detail.modifiedUtc ? formatDateTime(detail.modifiedUtc) : dash}
              </Row>

              <Row label="Repositories">
                {detail.appRepositories.length === 0 ? (
                  dash
                ) : (
                  <ul className={styles.repos}>
                    {detail.appRepositories.map((repo) => (
                      <li key={repo.id}>
                        {repo.repositoryTypeName ?? 'Unknown type'} —{' '}
                        <span className={styles.mono}>{repo.repositoryUrl}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </Row>
            </div>

            <div className={styles.actions}>
              <Button appearance="primary" icon={<EditRegular />} onClick={onEdit}>
                Edit
              </Button>
              <Button appearance="secondary" onClick={onClose}>
                Close
              </Button>
            </div>
          </>
        )}
      </DrawerBody>
    </OverlayDrawer>
  );
}
