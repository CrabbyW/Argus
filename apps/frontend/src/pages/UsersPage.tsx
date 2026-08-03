import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Switch,
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
import {
  AddRegular,
  ArrowUndoRegular,
  DeleteRegular,
  EditRegular,
  KeyRegular,
} from '@fluentui/react-icons';
import { api } from '../api/client';
import type { User, UserUpsert } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { useAppToast } from '../hooks/useAppToast';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSheetStyles } from '../styles/sheetStyles';

/** Mirrors `UserPasswordRules.MinimumLength` on the server, which enforces it regardless. */
const MINIMUM_PASSWORD_LENGTH = 8;

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '20px' },
  pageHeader: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  spacer: { flexGrow: 1 },
  muted: { color: tokens.colorNeutralForeground3 },
  form: { display: 'flex', flexDirection: 'column', rowGap: '12px' },
  actions: { display: 'flex', gap: '4px' },
  tableWrapper: { overflowX: 'auto' },
  disabledRow: { opacity: 0.55 },
  destructive: {
    color: tokens.colorPaletteRedForeground1,
    ':hover': {
      color: tokens.colorPaletteRedForeground1,
      backgroundColor: tokens.colorPaletteRedBackground1,
    },
  },
});

const blankForm: UserUpsert = { username: '', displayName: '', password: '' };

/** Local dates read better than a UTC string; the value itself stays UTC on the wire. */
function formatUtc(value: string | null): string {
  if (!value) {
    return '';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '' : parsed.toLocaleString();
}

/**
 * Who can sign in to Argus.
 *
 * Until now the answer was "whoever `DbSeeder` created the first time the database came up", and
 * changing that password meant deleting a row in SSMS — the seeder skips a table that already has
 * a user in it. This screen is that escape hatch, and the two guards behind it (you cannot disable
 * yourself, you cannot disable the last account) exist because the seeder cannot repair a locked
 * door: it fills an empty table, and a soft-deleted row is not empty.
 */
export function UsersPage() {
  const styles = useStyles();
  const sheet = useSheetStyles();
  const toast = useAppToast();
  const { user: currentUser } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  // In the URL like every other piece of grid state, so the view can be linked and reloaded.
  const includeDisabled = searchParams.get('disabled') === '1';

  const [items, setItems] = useState<User[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [editing, setEditing] = useState<{ id: number | null; form: UserUpsert } | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const [passwordFor, setPasswordFor] = useState<User | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [isSettingPassword, setIsSettingPassword] = useState(false);

  const [pendingDisable, setPendingDisable] = useState<User | null>(null);
  const [isDisabling, setIsDisabling] = useState(false);

  const load = useCallback(async (withDisabled: boolean) => {
    setIsLoading(true);
    setError(null);

    try {
      setItems(await api.getUsers(withDisabled));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load users.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(includeDisabled);
  }, [includeDisabled, load]);

  function setIncludeDisabled(next: boolean) {
    const params = new URLSearchParams(searchParams);

    if (next) {
      params.set('disabled', '1');
    } else {
      params.delete('disabled');
    }

    setSearchParams(params, { replace: true });
  }

  async function handleSave() {
    if (!editing) {
      return;
    }

    setIsSaving(true);

    try {
      const payload: UserUpsert = {
        username: editing.form.username.trim(),
        displayName: editing.form.displayName.trim(),
      };

      if (editing.id === null) {
        await api.createUser({ ...payload, password: editing.form.password });
        toast.success('User created.');
      } else {
        // No password here on purpose — the server ignores one, and this way the screen
        // never round-trips a credential it read.
        await api.updateUser(editing.id, payload);
        toast.success('User saved.');
      }

      setEditing(null);
      await load(includeDisabled);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to save the user.';
      toast.error('Save failed', message);
      setError(message);
    } finally {
      setIsSaving(false);
    }
  }

  async function handleSetPassword() {
    if (!passwordFor) {
      return;
    }

    setIsSettingPassword(true);

    try {
      await api.setUserPassword(passwordFor.id, newPassword);
      toast.success(`Password changed for ${passwordFor.username}.`);
      setPasswordFor(null);
      setNewPassword('');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to set the password.';
      toast.error('Password not changed', message);
    } finally {
      setIsSettingPassword(false);
    }
  }

  async function confirmDisable() {
    if (!pendingDisable) {
      return;
    }

    setIsDisabling(true);

    try {
      await api.disableUser(pendingDisable.id);
      toast.success(`${pendingDisable.username} can no longer sign in.`);
      setPendingDisable(null);
      await load(includeDisabled);
    } catch (err) {
      // The two lockout guards land here as a 400. The server's message names which rule
      // was hit, so it is shown rather than replaced with something generic.
      const message = err instanceof Error ? err.message : 'Failed to disable the user.';
      toast.error('Not disabled', message);
      setError(message);
    } finally {
      setIsDisabling(false);
    }
  }

  async function restore(user: User) {
    try {
      await api.restoreUser(user.id);
      toast.success(`${user.username} can sign in again.`);
      await load(includeDisabled);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to restore the user.';
      toast.error('Not restored', message);
    }
  }

  const isFormValid =
    editing !== null &&
    editing.form.username.trim().length > 0 &&
    editing.form.displayName.trim().length > 0 &&
    (editing.id !== null || (editing.form.password ?? '').length >= MINIMUM_PASSWORD_LENGTH);

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Users</Title3>
        <Text className={styles.muted}>
          {items.length} account{items.length === 1 ? '' : 's'}
        </Text>

        <div className={styles.spacer} />

        <Switch
          label="Show disabled"
          checked={includeDisabled}
          onChange={(_, data) => setIncludeDisabled(data.checked)}
        />

        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() => setEditing({ id: null, form: { ...blankForm } })}
        >
          New user
        </Button>
      </div>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {isLoading ? (
        <Spinner label="Loading users..." />
      ) : (
        <div className={styles.tableWrapper}>
          <Table size="small" className={sheet.table}>
            <colgroup>
              <col style={{ width: '70px' }} />
              <col style={{ width: '200px' }} />
              <col />
              <col style={{ width: '110px' }} />
              <col style={{ width: '170px' }} />
              <col style={{ width: '170px' }} />
              <col style={{ width: '140px' }} />
            </colgroup>

            <TableHeader>
              <TableRow>
                <TableHeaderCell className={sheet.headerCell}>Id</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Username</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Display name</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Active</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Created</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Last sign-in</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>

            <TableBody>
              {items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7}>
                    <span className={styles.muted}>No accounts.</span>
                  </TableCell>
                </TableRow>
              )}

              {items.map((user) => {
                const isSelf = currentUser?.username === user.username;

                return (
                  <TableRow key={user.id} className={user.isEnabled ? undefined : styles.disabledRow}>
                    <TableCell className={sheet.idCell}>{user.id}</TableCell>
                    <TableCell>
                      {user.username}
                      {isSelf && (
                        <>
                          {' '}
                          <Badge appearance="tint" color="brand" size="small">
                            you
                          </Badge>
                        </>
                      )}
                    </TableCell>
                    <TableCell>{user.displayName}</TableCell>
                    <TableCell>
                      {user.isEnabled ? (
                        <Badge appearance="tint" color="success" size="small">
                          Active
                        </Badge>
                      ) : (
                        <Badge appearance="tint" color="danger" size="small">
                          Disabled
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>{formatUtc(user.createdUtc)}</TableCell>
                    <TableCell>
                      {formatUtc(user.lastLoginUtc) || <span className={styles.muted}>never</span>}
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
                                id: user.id,
                                form: {
                                  username: user.username,
                                  displayName: user.displayName,
                                },
                              })
                            }
                          />
                        </Tooltip>

                        <Tooltip content="Set password" relationship="label">
                          <Button
                            size="small"
                            appearance="subtle"
                            icon={<KeyRegular />}
                            onClick={() => {
                              setPasswordFor(user);
                              setNewPassword('');
                            }}
                          />
                        </Tooltip>

                        {user.isEnabled ? (
                          <Tooltip
                            content={isSelf ? 'You cannot disable your own account' : 'Disable'}
                            relationship="label"
                          >
                            <Button
                              size="small"
                              appearance="subtle"
                              className={styles.destructive}
                              icon={<DeleteRegular />}
                              disabled={isSelf}
                              onClick={() => setPendingDisable(user)}
                            />
                          </Tooltip>
                        ) : (
                          <Tooltip content="Restore" relationship="label">
                            <Button
                              size="small"
                              appearance="subtle"
                              icon={<ArrowUndoRegular />}
                              onClick={() => void restore(user)}
                            />
                          </Tooltip>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {editing && (
        <Dialog open onOpenChange={(_, data) => !data.open && setEditing(null)}>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>{editing.id === null ? 'New user' : 'Edit user'}</DialogTitle>

              <DialogContent>
                <div className={styles.form}>
                  <Field label="Username" required>
                    <Input
                      value={editing.form.username}
                      onChange={(_, data) =>
                        setEditing({ ...editing, form: { ...editing.form, username: data.value } })
                      }
                    />
                  </Field>

                  <Field label="Display name" required>
                    <Input
                      value={editing.form.displayName}
                      onChange={(_, data) =>
                        setEditing({
                          ...editing,
                          form: { ...editing.form, displayName: data.value },
                        })
                      }
                    />
                  </Field>

                  {editing.id === null ? (
                    <Field
                      label="Password"
                      required
                      hint={`At least ${MINIMUM_PASSWORD_LENGTH} characters.`}
                    >
                      <Input
                        type="password"
                        value={editing.form.password ?? ''}
                        onChange={(_, data) =>
                          setEditing({
                            ...editing,
                            form: { ...editing.form, password: data.value },
                          })
                        }
                      />
                    </Field>
                  ) : (
                    <Text className={styles.muted}>
                      Passwords are changed with the key button, not here.
                    </Text>
                  )}
                </div>
              </DialogContent>

              <DialogActions>
                <Button appearance="secondary" onClick={() => setEditing(null)} disabled={isSaving}>
                  Cancel
                </Button>
                <Button
                  appearance="primary"
                  onClick={() => void handleSave()}
                  disabled={!isFormValid || isSaving}
                >
                  {isSaving ? 'Saving...' : 'Save'}
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}

      {passwordFor && (
        <Dialog open onOpenChange={(_, data) => !data.open && setPasswordFor(null)}>
          <DialogSurface>
            <DialogBody>
              <DialogTitle>Set password for {passwordFor.username}</DialogTitle>

              <DialogContent>
                <Field
                  label="New password"
                  required
                  hint={`At least ${MINIMUM_PASSWORD_LENGTH} characters. The old one is not needed.`}
                >
                  <Input
                    type="password"
                    value={newPassword}
                    onChange={(_, data) => setNewPassword(data.value)}
                  />
                </Field>
              </DialogContent>

              <DialogActions>
                <Button
                  appearance="secondary"
                  onClick={() => setPasswordFor(null)}
                  disabled={isSettingPassword}
                >
                  Cancel
                </Button>
                <Button
                  appearance="primary"
                  onClick={() => void handleSetPassword()}
                  disabled={newPassword.length < MINIMUM_PASSWORD_LENGTH || isSettingPassword}
                >
                  {isSettingPassword ? 'Saving...' : 'Set password'}
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      )}

      {pendingDisable && (
        <ConfirmDialog
          title="Disable this account?"
          message={`${pendingDisable.username} will no longer be able to sign in. The row is kept and can be restored.`}
          confirmLabel="Disable"
          isBusy={isDisabling}
          onConfirm={() => void confirmDisable()}
          onCancel={() => setPendingDisable(null)}
        />
      )}
    </div>
  );
}
