import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Badge,
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
import { itemsOf, useLookups } from '../hooks/useLookups';
import { useAppToast } from '../hooks/useAppToast';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSheetStyles } from '../styles/sheetStyles';

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
  badgeList: { display: 'flex', flexWrap: 'wrap', gap: '4px' },
  destructive: {
    color: tokens.colorPaletteRedForeground1,
    ':hover': {
      color: tokens.colorPaletteRedForeground1,
      backgroundColor: tokens.colorPaletteRedBackground1,
    },
  },
});

const blankForm: AppRepositoryUpsert = {
  repositoryUrl: '',
  repositoryTypeId: null,
  description: '',
  installationIds: [],
};

interface InstallationOption {
  id: number;
  label: string;
}

/**
 * A repository is linked to installations many-to-many: one row per source location, one link
 * per installation built from it. It is deliberately not owned by an application — a plain
 * foreign key would store the same URL once per installation, which is exactly the duplication
 * the normalized model exists to remove.
 *
 * The two filters are independent. "Installation" answers what one deployment is built from;
 * "Application" answers what an application uses anywhere it runs.
 */
export function RepositoriesPage() {
  const styles = useStyles();
  const sheet = useSheetStyles();
  const toast = useAppToast();
  const { lookups, error: lookupsError } = useLookups();
  const repositoryTypes = itemsOf(lookups, 'repositorytypes');

  const [items, setItems] = useState<AppRepository[]>([]);
  const [installations, setInstallations] = useState<InstallationOption[]>([]);
  const [appNameFilter, setAppNameFilter] = useState<number | null>(null);
  const [installationFilter, setInstallationFilter] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<{ id: number | null; form: AppRepositoryUpsert } | null>(
    null,
  );
  const [isSaving, setIsSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<AppRepository | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const load = useCallback(async (appNameId: number | null, installationId: number | null) => {
    setIsLoading(true);
    setError(null);

    try {
      setItems(await api.getRepositories({ appNameId, installationId }));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load repositories.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(appNameFilter, installationFilter);
  }, [appNameFilter, installationFilter, load]);

  // Installations are not a lookup, but linking needs a readable label for each one.
  // Decommissioned rows are included: a retired installation keeps the history of where its
  // code came from, and hiding it here would make that link uneditable.
  useEffect(() => {
    let cancelled = false;

    api
      .getInstallations({ pageNumber: 1, pageSize: 500, includeDisabled: true })
      .then((page) => {
        if (cancelled) {
          return;
        }

        setInstallations(
          page.items.map((item) => ({
            id: item.id,
            label: `${item.appName} (${item.appStageName}) on ${item.machineName}`,
          })),
        );
      })
      .catch((err) =>
        setError(err instanceof Error ? err.message : 'Failed to load installations.'),
      );

    return () => {
      cancelled = true;
    };
  }, []);

  const installationLabels = useMemo(
    () => new Map(installations.map((option) => [option.id, option.label])),
    [installations],
  );

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
      await load(appNameFilter, installationFilter);
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
      await load(appNameFilter, installationFilter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete the repository.';
      toast.error('Delete failed', message);
      setError(message);
    } finally {
      setIsDeleting(false);
    }
  }

  // An unattached repository is legitimate: it can be registered before its installation exists.
  const isValid = editing !== null && editing.form.repositoryUrl.trim().length > 0;

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
              form: {
                ...blankForm,
                installationIds: installationFilter ? [installationFilter] : [],
              },
            })
          }
        >
          New repository
        </Button>
      </div>

      {lookupsError && (
        <MessageBar intent="error">
          <MessageBarBody>Filters could not be loaded: {lookupsError}</MessageBarBody>
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
            selectedOptions={appNameFilter ? [String(appNameFilter)] : ['']}
            value={itemsOf(lookups, 'appnames').find((app) => app.id === appNameFilter)?.name ?? ''}
            onOptionSelect={(_, data) =>
              setAppNameFilter(data.optionValue ? Number(data.optionValue) : null)
            }
          >
            <Option value="">All applications</Option>
            {itemsOf(lookups, 'appnames').map((app) => (
              <Option key={app.id} value={String(app.id)}>
                {app.name}
              </Option>
            ))}
          </Dropdown>
        </Field>

        <Field label="Installation" className={styles.filter}>
          <Dropdown
            placeholder="All installations"
            selectedOptions={installationFilter ? [String(installationFilter)] : ['']}
            value={installationFilter ? (installationLabels.get(installationFilter) ?? '') : ''}
            onOptionSelect={(_, data) =>
              setInstallationFilter(data.optionValue ? Number(data.optionValue) : null)
            }
          >
            <Option value="">All installations</Option>
            {installations.map((option) => (
              <Option key={option.id} value={String(option.id)}>
                {option.label}
              </Option>
            ))}
          </Dropdown>
        </Field>
      </div>

      {isLoading ? (
        <Spinner label="Loading repositories..." />
      ) : (
        <div className={styles.tableWrapper}>
          <Table size="small" className={sheet.table}>
            <TableHeader>
              <TableRow>
                <TableHeaderCell className={sheet.headerCell}>Type</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>URL</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Installations</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Description</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>

            <TableBody>
              {items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5}>
                    <span className={styles.muted}>No repositories match the current filter.</span>
                  </TableCell>
                </TableRow>
              )}

              {items.map((repo) => (
                <TableRow key={repo.id}>
                  <TableCell>
                    {repo.repositoryTypeName ?? <span className={styles.muted}>not recorded</span>}
                  </TableCell>
                  <TableCell className={styles.mono}>{repo.repositoryUrl}</TableCell>
                  <TableCell>
                    {repo.installationIds.length === 0 ? (
                      <span className={styles.muted}>unattached</span>
                    ) : (
                      <div className={styles.badgeList}>
                        {repo.installationIds.map((id) => (
                          <Badge key={id} appearance="tint" color="informative" size="small">
                            {installationLabels.get(id) ?? `#${id}`}
                          </Badge>
                        ))}
                      </div>
                    )}
                  </TableCell>
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
                                repositoryUrl: repo.repositoryUrl,
                                repositoryTypeId: repo.repositoryTypeId ?? null,
                                description: repo.description ?? '',
                                installationIds: [...repo.installationIds],
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
                  {/* An Id-backed dropdown off the repositorytypes lookup, like every other
                      shared value. It used to be a hardcoded list mirroring a C# enum. */}
                  <Field label="Type" hint="Leave empty if the system is not known.">
                    <Dropdown
                      selectedOptions={
                        editing.form.repositoryTypeId === null ||
                        editing.form.repositoryTypeId === undefined
                          ? []
                          : [String(editing.form.repositoryTypeId)]
                      }
                      value={
                        repositoryTypes.find((type) => type.id === editing.form.repositoryTypeId)
                          ?.name ?? ''
                      }
                      placeholder="Not recorded"
                      onOptionSelect={(_, data) =>
                        setEditing({
                          ...editing,
                          form: {
                            ...editing.form,
                            repositoryTypeId: data.optionValue ? Number(data.optionValue) : null,
                          },
                        })
                      }
                    >
                      <Option value="">Not recorded</Option>
                      {repositoryTypes.map((type) => (
                        <Option key={type.id} value={String(type.id)}>
                          {type.name}
                        </Option>
                      ))}
                    </Dropdown>
                  </Field>

                  <Field label="URL" required>
                    <Input
                      value={editing.form.repositoryUrl}
                      maxLength={512}
                      onChange={(_, data) =>
                        setEditing({
                          ...editing,
                          form: { ...editing.form, repositoryUrl: data.value },
                        })
                      }
                      placeholder="git://server/project.git"
                    />
                  </Field>

                  <Field
                    label="Installations"
                    hint="Leave empty to register the repository before its installation exists."
                  >
                    <Dropdown
                      multiselect
                      placeholder="Not linked to any installation"
                      selectedOptions={editing.form.installationIds.map(String)}
                      value={editing.form.installationIds
                        .map((id) => installationLabels.get(id) ?? `#${id}`)
                        .join(', ')}
                      onOptionSelect={(_, data) =>
                        setEditing({
                          ...editing,
                          form: {
                            ...editing.form,
                            installationIds: data.selectedOptions.map(Number),
                          },
                        })
                      }
                    >
                      {installations.map((option) => (
                        <Option key={option.id} value={String(option.id)}>
                          {option.label}
                        </Option>
                      ))}
                    </Dropdown>
                  </Field>

                  <Field label="Description">
                    <Input
                      value={editing.form.description ?? ''}
                      maxLength={512}
                      onChange={(_, data) =>
                        setEditing({
                          ...editing,
                          form: { ...editing.form, description: data.value },
                        })
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
          message={`Remove ${pendingDelete.repositoryUrl}? Its links to ${pendingDelete.installationIds.length} installation(s) go with it; the installations themselves are untouched.`}
          confirmLabel="Remove"
          isBusy={isDeleting}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
