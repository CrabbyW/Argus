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
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { DismissRegular, EditRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { InstallationDetail } from '../api/types';

const useStyles = makeStyles({
  // Wide enough for a physical path on one line, narrow enough to leave the grid readable
  // behind it — the point of a drawer over a dialog is that the row stays in view.
  surface: { width: '460px', maxWidth: '100vw' },

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

  useEffect(() => {
    let cancelled = false;

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
                {detail.validFromDate} → {detail.validToDate ?? 'open'}
              </Row>
              <Row label="Status">
                <Badge appearance="filled" color={detail.isActive ? 'success' : 'informative'}>
                  {detail.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </Row>
              <Row label="Created">{new Date(detail.createdUtc).toLocaleString()}</Row>
              <Row label="Modified">
                {detail.modifiedUtc ? new Date(detail.modifiedUtc).toLocaleString() : dash}
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
