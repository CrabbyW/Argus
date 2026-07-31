import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Switch,
  Tab,
  TabList,
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
import { AddRegular, DeleteRegular, DismissRegular, EditRegular, SaveRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { EditableLookupKind, LookupItem, LookupKind } from '../api/types';
import { lookupMaxNameLength } from '../api/types';
import { useAppToast } from '../hooks/useAppToast';
import { ConfirmDialog } from '../components/ConfirmDialog';

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '20px' },
  pageHeader: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  spacer: { flexGrow: 1 },
  muted: { color: tokens.colorNeutralForeground3 },
  editorCard: {
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '16px',
    display: 'flex',
    gap: '12px',
    flexWrap: 'wrap',
    alignItems: 'end',
  },
  grow: { flex: '1 1 220px' },
  tabStrip: { overflowX: 'auto', flexShrink: 0 },
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

interface TabDefinition {
  kind: EditableLookupKind;
  label: string;
  /** Singular, for the "New …" button and the delete prompt. */
  singular: string;
  hasDescription: boolean;
  hasSortOrder: boolean;
  hasLoadBalancer: boolean;
}

/**
 * The eight editable lookups from the roadplan, in the order they are filled.
 *
 * AppRepositories is the ninth shared value but is not here: it carries a repository type and
 * its own installation links, which the generic editor below cannot express. It has its own
 * screen at /repositories.
 */
const tabs: TabDefinition[] = [
  { kind: 'machines', label: 'Machines', singular: 'machine', hasDescription: true, hasSortOrder: false, hasLoadBalancer: false },
  { kind: 'appnames', label: 'Applications', singular: 'application', hasDescription: true, hasSortOrder: false, hasLoadBalancer: false },
  { kind: 'appstagenames', label: 'Stages', singular: 'stage', hasDescription: false, hasSortOrder: true, hasLoadBalancer: false },
  { kind: 'processorarchitectures', label: 'Architectures', singular: 'architecture', hasDescription: false, hasSortOrder: false, hasLoadBalancer: false },
  { kind: 'dnsendpoints', label: 'DNS endpoints', singular: 'DNS endpoint', hasDescription: true, hasSortOrder: false, hasLoadBalancer: true },
  { kind: 'rootpaths', label: 'Root paths', singular: 'root path', hasDescription: false, hasSortOrder: false, hasLoadBalancer: false },
  { kind: 'physicalpaths', label: 'Physical paths', singular: 'physical path', hasDescription: false, hasSortOrder: false, hasLoadBalancer: false },
  { kind: 'tags', label: 'Tags', singular: 'tag', hasDescription: true, hasSortOrder: false, hasLoadBalancer: false },
];

interface EditorState {
  id: number | null;
  name: string;
  description: string;
  sortOrder: number;
  isLoadBalancer: boolean;
}

const blankEditor: EditorState = {
  id: null,
  name: '',
  description: '',
  sortOrder: 0,
  isLoadBalancer: false,
};

export function LookupsPage() {
  const styles = useStyles();
  const navigate = useNavigate();
  const toast = useAppToast();
  const { kind: routeKind } = useParams();

  // The tab lives in the URL, so a lookup screen can be linked to directly.
  const tab = tabs.find((t) => t.kind === routeKind) ?? tabs[0];
  const activeKind: EditableLookupKind = tab.kind;

  const [items, setItems] = useState<LookupItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<LookupItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // Id + Name + Actions, plus whichever optional columns this tab shows.
  const columnCount =
    3 + Number(tab.hasDescription) + Number(tab.hasSortOrder) + Number(tab.hasLoadBalancer);

  const load = useCallback(async (kind: LookupKind) => {
    setIsLoading(true);
    setError(null);

    try {
      setItems(await api.getLookup(kind));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load the lookup.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    setEditor(null);
    void load(activeKind);
  }, [activeKind, load]);

  async function handleSave() {
    if (!editor || !editor.name.trim()) {
      return;
    }

    setIsSaving(true);
    setError(null);

    const payload = {
      name: editor.name.trim(),
      description: editor.description.trim() ? editor.description.trim() : null,
      sortOrder: editor.sortOrder,
      isLoadBalancer: editor.isLoadBalancer,
    };

    try {
      if (editor.id === null) {
        await api.createLookupItem(activeKind, payload);
        toast.success(`Added "${payload.name}".`);
      } else {
        // Renaming here is the single-source-of-truth edit: every installation
        // pointing at this Id shows the new name at once.
        await api.updateLookupItem(activeKind, editor.id, payload);
        toast.success(`Saved "${payload.name}".`);
      }

      setEditor(null);
      await load(activeKind);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to save.';
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
    setError(null);

    try {
      await api.deleteLookupItem(activeKind, pendingDelete.id);
      toast.success(`Removed "${pendingDelete.name}".`);
      setPendingDelete(null);
      await load(activeKind);
    } catch (err) {
      // The API refuses when installations still reference this row.
      const message = err instanceof Error ? err.message : 'Failed to delete.';
      toast.error('Cannot remove', message);
      setError(message);
      setPendingDelete(null);
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Lookups</Title3>
        <Text className={styles.muted}>
          {items.length} record{items.length === 1 ? '' : 's'} in {tab.label.toLowerCase()}
        </Text>
        <div className={styles.spacer} />
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() => setEditor({ ...blankEditor })}
        >
          New {tab.singular}
        </Button>
      </div>

      {/* Eight tabs do not fit a narrow window; the strip scrolls rather than wrapping into
          two rows that push the table down the page. */}
      <TabList
        className={styles.tabStrip}
        selectedValue={activeKind}
        onTabSelect={(_, data) => navigate(`/lookups/${data.value}`)}
      >
        {tabs.map((t) => (
          <Tab key={t.kind} value={t.kind}>
            {t.label}
          </Tab>
        ))}
      </TabList>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {editor && (
        <div className={styles.editorCard}>
          {/* maxLength mirrors the column width the server validates against, so an over-long
              name is stopped here rather than coming back as a 400. */}
          <Field
            label="Name"
            required
            className={styles.grow}
            hint={`Up to ${lookupMaxNameLength[activeKind]} characters.`}
          >
            <Input
              value={editor.name}
              maxLength={lookupMaxNameLength[activeKind]}
              onChange={(_, data) => setEditor({ ...editor, name: data.value })}
              autoFocus
            />
          </Field>

          {tab.hasDescription && (
            <Field label="Description" className={styles.grow}>
              <Input
                value={editor.description}
                onChange={(_, data) => setEditor({ ...editor, description: data.value })}
              />
            </Field>
          )}

          {tab.hasSortOrder && (
            <Field label="Sort order" hint="Controls the order stages appear in.">
              <Input
                type="number"
                value={String(editor.sortOrder)}
                onChange={(_, data) => setEditor({ ...editor, sortOrder: Number(data.value) || 0 })}
              />
            </Field>
          )}

          {tab.hasLoadBalancer && (
            <Switch
              checked={editor.isLoadBalancer}
              onChange={(_, data) => setEditor({ ...editor, isLoadBalancer: data.checked })}
              label="Load balancer"
            />
          )}

          <Button
            appearance="primary"
            icon={<SaveRegular />}
            onClick={() => void handleSave()}
            disabled={isSaving || !editor.name.trim()}
          >
            Save
          </Button>
          <Button icon={<DismissRegular />} onClick={() => setEditor(null)} disabled={isSaving}>
            Cancel
          </Button>
        </div>
      )}

      {isLoading ? (
        <Spinner label="Loading..." />
      ) : (
        <div className={styles.tableWrapper}>
          <Table size="small">
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Id</TableHeaderCell>
                <TableHeaderCell>Name</TableHeaderCell>
                {tab.hasDescription && <TableHeaderCell>Description</TableHeaderCell>}
                {tab.hasSortOrder && <TableHeaderCell>Sort order</TableHeaderCell>}
                {tab.hasLoadBalancer && <TableHeaderCell>Load balancer</TableHeaderCell>}
                <TableHeaderCell>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>

            <TableBody>
              {items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={columnCount}>
                    <span className={styles.muted}>No items yet.</span>
                  </TableCell>
                </TableRow>
              )}

              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className={styles.muted}>{item.id}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  {tab.hasDescription && (
                    <TableCell>{item.description ?? <span className={styles.muted}>—</span>}</TableCell>
                  )}
                  {tab.hasSortOrder && <TableCell>{item.sortOrder}</TableCell>}
                  {tab.hasLoadBalancer && (
                    <TableCell>
                      {item.isLoadBalancer ? (
                        <Badge appearance="filled" color="brand">
                          Load balancer
                        </Badge>
                      ) : (
                        <span className={styles.muted}>—</span>
                      )}
                    </TableCell>
                  )}
                  <TableCell>
                    <div className={styles.actions}>
                      <Tooltip content="Edit" relationship="label">
                        <Button
                          size="small"
                          appearance="subtle"
                          icon={<EditRegular />}
                          onClick={() =>
                            setEditor({
                              id: item.id,
                              name: item.name,
                              description: item.description ?? '',
                              // Carried over from the loaded row: the save above sends the whole
                              // DTO, so defaulting these would wipe the stage ordering and the
                              // load-balancer flag on every rename.
                              sortOrder: item.sortOrder,
                              isLoadBalancer: item.isLoadBalancer,
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
                          onClick={() => setPendingDelete(item)}
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

      {pendingDelete && (
        <ConfirmDialog
          title={`Remove ${pendingDelete.name}`}
          message={`Remove the ${tab.singular} "${pendingDelete.name}"? Installations still pointing at it will block this.`}
          confirmLabel="Remove"
          isBusy={isDeleting}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
