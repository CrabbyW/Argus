import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { EditRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { InstallationDetail } from '../api/types';
import { repositoryTypeNames } from '../api/types';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'minmax(140px, auto) 1fr',
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
  full: { gridColumn: '1 / -1' },
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

/** Read-only view, so answering "what is deployed here?" does not mean opening an edit form. */
export function InstallationDetailDialog({ installationId, onClose, onEdit }: Props) {
  const styles = useStyles();

  const [detail, setDetail] = useState<InstallationDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

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
    <Dialog open onOpenChange={(_, data) => !data.open && onClose()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>
            {detail ? `${detail.appName} on ${detail.machineName}` : 'Installation'}
          </DialogTitle>

          <DialogContent>
            {error && (
              <MessageBar intent="error">
                <MessageBarBody>{error}</MessageBarBody>
              </MessageBar>
            )}

            {!detail && !error && <Spinner label="Loading..." />}

            {detail && (
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
                          {repositoryTypeNames[repo.repositoryType] ?? 'Unknown'} —{' '}
                          <span className={styles.mono}>{repo.repositoryUrl}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </Row>
              </div>
            )}
          </DialogContent>

          <DialogActions>
            <Button appearance="secondary" onClick={onClose}>
              Close
            </Button>
            <Button appearance="primary" icon={<EditRegular />} onClick={onEdit} disabled={!detail}>
              Edit
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
