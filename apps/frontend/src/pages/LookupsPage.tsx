import { useCallback, useEffect, useMemo, useState } from 'react';
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
import type { LookupItem, LookupKind } from '../api/types';
import { useAppToast } from '../hooks/useAppToast';
import { useLookupMetadata } from '../hooks/useLookupMetadata';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSheetStyles } from '../styles/sheetStyles';

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
  const sheet = useSheetStyles();
  const navigate = useNavigate();
  const toast = useAppToast();
  const { kind: routeKind } = useParams();

  const { metadata, error: metadataError } = useLookupMetadata();

  /**
   * The tabs, straight from the server. Read-only kinds are dropped: AppRepositories carries a
   * type and its own installation links, which the generic editor below cannot express, and it
   * has its own screen at /repositories. Everything else appears here on its own — a lookup added
   * on the server needs no change to this file.
   */
  const tabs = useMemo(() => metadata.filter((meta) => !meta.isReadOnly), [metadata]);

  // The tab lives in the URL, so a lookup screen can be linked to directly. Undefined until the
  // metadata request comes back.
  const tab = tabs.find((t) => t.kind === routeKind) ?? tabs[0];
  const activeKind: LookupKind | undefined = tab?.kind;

  const [items, setItems] = useState<LookupItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editor, setEditor] = useState<EditorState | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<LookupItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // Id + Name + Actions, plus whichever optional columns this tab shows.
  const columnCount =
    3 + Number(tab?.hasDescription) + Number(tab?.hasSortOrder) + Number(tab?.hasLoadBalancer);

  /**
   * Numbered from 1 down, as the source sheets are. The server orders lookups by name — right for
   * the dropdowns it also feeds, wrong here, where the Id is the first column and a list that
   * reads 2, 4, 1, 3 looks like a mistake. Sorted on the client so the dropdowns stay alphabetical.
   */
  const rows = useMemo(() => [...items].sort((a, b) => a.id - b.id), [items]);

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

    // Nothing to load until the metadata says which kinds exist.
    if (activeKind) {
      void load(activeKind);
    }
  }, [activeKind, load]);

  async function handleSave() {
    if (!editor || !editor.name.trim() || !activeKind) {
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
    if (!pendingDelete || !activeKind) {
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
          {items.length} record{items.length === 1 ? '' : 's'} in {tab?.label.toLowerCase()}
        </Text>
        <div className={styles.spacer} />
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() => setEditor({ ...blankEditor })}
        >
          New {tab?.singular}
        </Button>
      </div>

      {/* The tabs do not fit a narrow window; the strip scrolls rather than wrapping into
          two rows that push the table down the page. */}
      <TabList
        className={styles.tabStrip}
        // Empty string, not undefined, while the metadata is still loading: an undefined value
        // makes this an uncontrolled TabList for one render and a controlled one afterwards,
        // which React warns about. No tab matches "", which is correct — there are none yet.
        selectedValue={activeKind ?? ''}
        onTabSelect={(_, data) => navigate(`/lookups/${data.value}`)}
      >
        {tabs.map((t) => (
          <Tab key={t.kind} value={t.kind}>
            {t.label}
          </Tab>
        ))}
      </TabList>

      {/* Surfaced rather than swallowed: with no metadata the screen has no tabs at all, and an
          empty strip reads as "there are no lookups" instead of "the request failed". */}
      {metadataError && (
        <MessageBar intent="error">
          <MessageBarBody>{metadataError}</MessageBarBody>
        </MessageBar>
      )}

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
            hint={`Up to ${tab?.maxNameLength} characters.`}
          >
            <Input
              value={editor.name}
              maxLength={tab?.maxNameLength}
              onChange={(_, data) => setEditor({ ...editor, name: data.value })}
              autoFocus
            />
          </Field>

          {tab?.hasDescription && (
            <Field label="Description" className={styles.grow}>
              <Input
                value={editor.description}
                onChange={(_, data) => setEditor({ ...editor, description: data.value })}
              />
            </Field>
          )}

          {tab?.hasSortOrder && (
            <Field label="Sort order" hint="Controls the order stages appear in.">
              <Input
                type="number"
                value={String(editor.sortOrder)}
                onChange={(_, data) => setEditor({ ...editor, sortOrder: Number(data.value) || 0 })}
              />
            </Field>
          )}

          {tab?.hasLoadBalancer && (
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
          <Table size="small" className={sheet.table}>
            {/*
              Fluent's Table is `table-layout: fixed`, so without this the four columns each take
              a quarter and the Id column ends up wider than the names it numbers. Id is as narrow
              as a number needs; Name and Description take what is left, as on the source sheet.
            */}
            <colgroup>
              <col style={{ width: '70px' }} />
              <col style={{ width: '260px' }} />
              {tab?.hasDescription && <col />}
              {tab?.hasSortOrder && <col style={{ width: '110px' }} />}
              {tab?.hasLoadBalancer && <col style={{ width: '140px' }} />}
              <col style={{ width: '110px' }} />
            </colgroup>

            <TableHeader>
              {/* Id first, then the name — the same column order as the source workbook's
                  lookup sheets, which is what these screens replace. */}
              <TableRow>
                <TableHeaderCell className={sheet.headerCell}>Id</TableHeaderCell>
                <TableHeaderCell className={sheet.headerCell}>Name</TableHeaderCell>
                {tab?.hasDescription && <TableHeaderCell className={sheet.headerCell}>Description</TableHeaderCell>}
                {tab?.hasSortOrder && <TableHeaderCell className={sheet.headerCell}>Sort order</TableHeaderCell>}
                {tab?.hasLoadBalancer && <TableHeaderCell className={sheet.headerCell}>Load balancer</TableHeaderCell>}
                <TableHeaderCell className={sheet.headerCell}>Actions</TableHeaderCell>
              </TableRow>
            </TableHeader>

            <TableBody>
              {rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={columnCount}>
                    <span className={styles.muted}>No items yet.</span>
                  </TableCell>
                </TableRow>
              )}

              {rows.map((item) => (
                <TableRow key={item.id}>
                  <TableCell className={sheet.idCell}>{item.id}</TableCell>
                  <TableCell>{item.name}</TableCell>
                  {tab?.hasDescription && (
                    <TableCell>{item.description ?? <span className={styles.muted}>—</span>}</TableCell>
                  )}
                  {tab?.hasSortOrder && <TableCell>{item.sortOrder}</TableCell>}
                  {tab?.hasLoadBalancer && (
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
          message={`Remove the ${tab?.singular} "${pendingDelete.name}"? Installations still pointing at it will block this.`}
          confirmLabel="Remove"
          isBusy={isDeleting}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
