import { useCallback, useEffect, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Title3,
  Tooltip,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { AddRegular, DeleteRegular, EditRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { AppRepository, AppRepositoryUpsert } from '../api/types';
import { repositoryTypeNames } from '../api/types';
import { useLookups } from '../hooks/useLookups';
import { useAppToast } from '../hooks/useAppToast';
import { ConfirmDialog } from '../components/ConfirmDialog';

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '20px' },
  pageHeader: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  spacer: { flexGrow: 1 },
  muted: { color: tokens.colorNeutralForeground3 },
  filterRow: { display: 'flex', gap: '12px', alignItems: 'end', flexWrap: 'wrap' },
  filter: { minWidth: '220px' },
  form: { display: 'flex', flexDirection: 'column', rowGap: '12px' },
  mono: { fontFamily: tokens.fontFamilyMonospace, fontSize: tokens.fontSizeBase200 },
  actions: { display: 'flex', gap: '4px' },
  tableWrapper: { overflowX: 'auto' },
  destructive: {
    color: tokens.colorPaletteRedForeground1,
    ':hover': {
      color: tokens.colorPaletteRedForeground1,
      backgroundColor: tokens.colorPaletteRedBackground1,
    },
  },
});

const blankForm: AppRepositoryUpsert = {
  applicationId: 0,
  repositoryUrl: '',
  repositoryType: 0,
  description: '',
};

/**
 * Repositories belong to an Application, not to one installation — the same source location
 * backs every deployment of that application. `roadplan` lists AppRepositories as an
 * installation attribute; this is where they are maintained.
 */
export function RepositoriesPage() {
  const styles = useStyles();
  const toast = useAppToast();
  const { lookups, error: lookupsError } = useLookups();

  const [items, setItems] = useState<AppRepository[]>([]);
  const [applicationFilter, setApplicationFilter] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<{ id: number | null; form: AppRepositoryUpsert } | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<AppRepository | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const load = useCallback(async (applicationId: number | null) => {
    setIsLoading(true);
    setError(null);

    try {
      setItems(await api.getRepositories(applicationId));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load repositories.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(applicationFilter);
  }, [applicationFilter, load]);

  function applicationName(id: number) {
    return lookups.applications.find((app) => app.id === id)?.name ?? `#${id}`;
  }

  async function handleSave() {
    if (!editing) {
      return;
    }

    setIsSaving(true);

    try {
      const payload: AppRepositoryUpsert = {
        ...editing.form,
        repositoryUrl: editing.form.repositoryUrl.trim(),
        description: editing.form.description?.trim() ? editing.form.description : null,
      };

      if (editing.id === null) {
        await api.createRepository(payload);
        toast.success('Repository added.');
      } else {
        await api.updateRepository(editing.id, payload);
        toast.success('Repository saved.');
      }

      setEditing(null);
      await load(applicationFilter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to save the repository.';
      toast.error('Save failed', message);
      setError(message);
    } finally {
      setIsSaving(false);
    }
  }

  async function confirmDelete() {
    if (!pendingDelete) {
      return;
    }

    setIsDeleting(true);

    try {
      await api.deleteRepository(pendingDelete.id);
      toast.success('Repository removed.');
      setPendingDelete(null);
      await load(applicationFilter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete the repository.';
      toast.error('Delete failed', message);
      setError(message);
    } finally {
      setIsDeleting(false);
    }
  }

  const isValid =
    editing !== null &&
    editing.form.applicationId > 0 &&
    editing.form.repositoryUrl.trim().length > 0;

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Repositories</Title3>
        <Text className={styles.muted}>
          {items.length} record{items.length === 1 ? '' : 's'}
        </Text>
        <div className={styles.spacer} />
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() =>
            setEditing({
              id: null,
              form: { ...blankForm, applicationId: applicationFilter ?? 0 },
            })
          }
        >
          New repository
        </Button>
      </div>

      {lookupsError && (
        <MessageBar intent="error">
          <MessageBarBody>Applications could not be loaded: {lookupsError}</MessageBarBody>
        </MessageBar>
      )}

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.filterRow}>
        <Field label="Application" className={styles.filter}>
          <Dropdown
            placeholder="All applications"
            selectedOptions={applicationFilter ? [String(applicationFilter)] : ['']}
            value={
              lookups.applications.find((app) => app.id === applicationFilter)?.name ?? ''
            }
            onOptionSelect={(_, data) =>
              setApplicationFilter(data.optionValue ? Number(data.optionValue) : null)
            }
          >
            <Option value="">All applications</Option>
            {lookups.applications.map((app) => (
              <Option key={app.id} value={String(app.id)}>
                {app.name}
              </Option>
            ))}
          </Dropdown>
        </Field>
      </div>

      {isLoading ? (
        <Spinner label="Loading repositories..." />
      ) : (
        <div className={styles.tableWrapper}>
          <Table size="small">
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Application</TableHeaderCell>
                <TableHeaderCell>Type</TableHeaderCell>
                <TableHeaderCell>URL</TableHeaderCell>
                <TableHeaderCell>Description</TableHeaderCell>
                <TableHeaderCell>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>

            <TableBody>
              {items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5}>
                    <span className={styles.muted}>No repositories yet.</span>
                  </TableCell>
                </TableRow>
              )}

              {items.map((repo) => (
                <TableRow key={repo.id}>
                  <TableCell>{applicationName(repo.applicationId)}</TableCell>
                  <TableCell>{repositoryTypeNames[repo.repositoryType] ?? 'Unknown'}</TableCell>
                  <TableCell className={styles.mono}>{repo.repositoryUrl}</TableCell>
                  <TableCell>
                    {repo.description ?? <span className={styles.muted}>—</span>}
                  </TableCell>
                  <TableCell>
                    <div className={styles.actions}>
                      <Tooltip content="Edit" relationship="label">
                        <Button
                          size="small"
                          appearance="subtle"
                          icon={<EditRegular />}
                          onClick={() =>
                            setEditing({
                              id: repo.id,
                              form: {
                                applicationId: repo.applicationId,
                                repositoryUrl: repo.repositoryUrl,
                                repositoryType: repo.repositoryType,
                                description: repo.description ?? '',
                              },
                            })
                          }
                        />
                      </Tooltip>
                      <Tooltip content="Remove" relationship="label">
                        <Button
                          size="small"
                          appearance="subtle"
                          className={styles.destructive}
                          icon={<DeleteRegular />}
                          onClick={() => setPendingDelete(repo)}
                        />
                      </Tooltip>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      {editing && (
        <Dialog open onOpenChange={(_, data) => !data.open && setEditing(null)}>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>{editing.id === null ? 'New repository' : 'Edit repository'}</DialogTitle>

              <DialogContent>
                <div className={styles.form}>
                  <Field label="Application" required>
                    <Dropdown
                      placeholder="Select application"
                      selectedOptions={
                        editing.form.applicationId ? [String(editing.form.applicationId)] : []
                      }
                      value={
                        lookups.applications.find((app) => app.id === editing.form.applicationId)
                          ?.name ?? ''
                      }
                      onOptionSelect={(_, data) =>
                        setEditing({
                          ...editing,
                          form: { ...editing.form, applicationId: Number(data.optionValue) || 0 },
                        })
                      }
                    >
                      {lookups.applications.map((app) => (
                        <Option key={app.id} value={String(app.id)}>
                          {app.name}
                        </Option>
                      ))}
                    </Dropdown>
                  </Field>

                  <Field label="Type">
                    <Dropdown
                      selectedOptions={[String(editing.form.repositoryType)]}
                      value={repositoryTypeNames[editing.form.repositoryType] ?? 'Unknown'}
                      onOptionSelect={(_, data) =>
                        setEditing({
                          ...editing,
                          form: { ...editing.form, repositoryType: Number(data.optionValue) },
                        })
                      }
                    >
                      {Object.entries(repositoryTypeNames).map(([value, label]) => (
                        <Option key={value} value={value}>
                          {label}
                        </Option>
                      ))}
                    </Dropdown>
                  </Field>

                  <Field label="URL" required>
                    <Input
                      value={editing.form.repositoryUrl}
                      onChange={(_, data) =>
                        setEditing({ ...editing, form: { ...editing.form, repositoryUrl: data.value } })
                      }
                      placeholder="git://server/project.git"
                    />
                  </Field>

                  <Field label="Description">
                    <Input
                      value={editing.form.description ?? ''}
                      onChange={(_, data) =>
                        setEditing({ ...editing, form: { ...editing.form, description: data.value } })
                      }
                    />
                  </Field>
                </div>
              </DialogContent>

              <DialogActions>
                <Button appearance="secondary" onClick={() => setEditing(null)} disabled={isSaving}>
                  Cancel
                </Button>
                <Button
                  appearance="primary"
                  onClick={() => void handleSave()}
                  disabled={isSaving || !isValid}
                >
                  {isSaving ? <Spinner size="tiny" label="Saving..." /> : 'Save'}
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}

      {pendingDelete && (
        <ConfirmDialog
          title="Remove repository"
          message={`Remove ${pendingDelete.repositoryUrl} from ${applicationName(pendingDelete.applicationId)}?`}
          confirmLabel="Remove"
          isBusy={isDeleting}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
