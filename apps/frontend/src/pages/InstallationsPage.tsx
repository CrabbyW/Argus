import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
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
  Tag,
  TagGroup,
  Text,
  Title3,
  Tooltip,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import {
  AddRegular,
  ArrowClockwiseRegular,
  ChevronDownRegular,
  ChevronUpRegular,
  DeleteRegular,
  EditRegular,
  EyeRegular,
  FilterRegular,
  TextNumberFormatRegular,
} from '@fluentui/react-icons';
import { api } from '../api/client';
import type { DataViewOutput, InstallationFilter, InstallationListItem } from '../api/types';
import { itemsOf, useLookups } from '../hooks/useLookups';
import { useAppToast } from '../hooks/useAppToast';
import { InstallationDialog } from '../components/InstallationDialog';
import { InstallationDetailDialog } from '../components/InstallationDetailDialog';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSheetStyles } from '../styles/sheetStyles';

/**
 * Two column sets, because the grid has two jobs.
 *
 * `ids` is the default and is the source workbook's ApplicationInstalation sheet, column for
 * column: the row's own Id followed by nothing but foreign keys. That is the roadplan's rule made
 * visible — every shared value lives in a lookup and the fact table only points at them.
 *
 * `names` resolves those references for reading. The roadplan's success criteria are questions in
 * words ("which machines serve paha.ga.local?"), and `2` does not answer one.
 *
 * Fluent's Table is `table-layout: fixed`, so these widths are the only thing setting column size.
 * Id columns are narrow because a number is narrow; name columns need the room or they truncate.
 */
const ID_COLUMNS = [
  // The row-number gutter, as on a spreadsheet: position in the result, not the record's Id.
  { key: 'rowNumber', width: 44 },
  { key: 'id', width: 60 },
  { key: 'machineId', width: 100 },
  { key: 'appNameId', width: 100 },
  { key: 'appStageNameId', width: 125 },
  { key: 'processorArchitectureId', width: 165 },
  { key: 'dnsEndpointId', width: 120 },
  { key: 'rootPathId', width: 100 },
  { key: 'physicalPathId', width: 120 },
  { key: 'tags', width: 150 },
  { key: 'valid', width: 170 },
  { key: 'active', width: 85 },
  { key: 'actions', width: 110 },
];

// Sized against the real seed values at 1600px — the longest of each (Data Exchange WebApi,
// https://vipsprava.1220.cz, c:\inetpub\callcenter.rc0) fits without truncating, and the total
// still leaves Active and Actions on screen rather than off the right edge.
const NAME_COLUMNS = [
  { key: 'rowNumber', width: 44 },
  { key: 'id', width: 55 },
  { key: 'machine', width: 125 },
  { key: 'application', width: 165 },
  { key: 'stage', width: 85 },
  { key: 'arch', width: 55 },
  { key: 'dns', width: 185 },
  { key: 'rootPath', width: 145 },
  { key: 'physicalPath', width: 185 },
  { key: 'tags', width: 105 },
  { key: 'valid', width: 140 },
  { key: 'active', width: 80 },
  { key: 'actions', width: 105 },
];

const widthOf = (columns: { width: number }[]) =>
  columns.reduce((total, column) => total + column.width, 0);

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '20px' },

  pageHeader: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  spacer: { flexGrow: 1 },
  muted: { color: tokens.colorNeutralForeground3 },

  filterCard: {
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '12px',
    display: 'flex',
    flexDirection: 'column',
    rowGap: '10px',
  },
  // The everyday row: search plus the facets that answer the roadplan's three headline questions.
  // Everything rarer lives behind the "More filters" disclosure so the grid starts near the top.
  filterBar: { display: 'flex', alignItems: 'flex-end', gap: '8px', flexWrap: 'wrap' },
  searchField: { flexGrow: 1, minWidth: '220px' },
  // Every control gets a label, so a narrow window drops columns instead of jumbling them.
  filterGrid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '12px',
  },
  // Fluent gives Dropdown a 250px minimum, which is wider than a grid track — the control then
  // refuses to shrink and the whole row pushes out past the card's right edge.
  dropdown: { minWidth: 0 },
  // A facet in the everyday row must not stretch to fill the leftover space; the search box does.
  barDropdown: { minWidth: 0, width: '160px' },
  filterFooter: { display: 'flex', alignItems: 'center', gap: '16px', flexWrap: 'wrap' },
  // Collapsed filters must never hide an active one, or the grid silently lies about its
  // contents. Anything set behind the disclosure comes back out here as a dismissable chip.
  chipRow: { display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' },
  advancedPanel: {
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    paddingTop: '12px',
    display: 'flex',
    flexDirection: 'column',
    rowGap: '12px',
  },

  tableWrapper: { overflowX: 'auto', position: 'relative' },
  // The sheet ruling itself comes from useSheetStyles; the width is set per view below.
  // A path is longer than any column that still leaves room for the other ten. Clip it and keep
  // the full value on hover.
  truncate: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  // Loading dims the grid instead of unmounting it — otherwise it flickers on every keystroke.
  dimmed: { opacity: 0.45, transition: 'opacity 120ms ease' },
  loadingBar: { position: 'absolute', top: '8px', right: '8px', zIndex: 1 },

  sortButton: {
    background: 'none',
    border: 'none',
    padding: 0,
    margin: 0,
    font: 'inherit',
    color: 'inherit',
    cursor: 'pointer',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    ':hover': { textDecoration: 'underline' },
    ':focus-visible': { outline: `2px solid ${tokens.colorStrokeFocus2}`, outlineOffset: '2px' },
  },
  mono: { fontFamily: tokens.fontFamilyMonospace, fontSize: tokens.fontSizeBase200 },
  // Tags are a set, not a sentence — badges wrap inside the cell rather than running past it.
  tagList: { display: 'flex', flexWrap: 'wrap', gap: '4px' },
  nowrap: { whiteSpace: 'nowrap' },
  rowActions: { display: 'flex', gap: '4px' },
  // A destructive action must not look identical to Edit sitting next to it.
  destructive: {
    color: tokens.colorPaletteRedForeground1,
    ':hover': {
      color: tokens.colorPaletteRedForeground1,
      backgroundColor: tokens.colorPaletteRedBackground1,
    },
  },
  footer: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
});

const emptyPage: DataViewOutput<InstallationListItem> = {
  items: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: 25,
  totalPages: 0,
};

const DEFAULT_SORT = 'machineName';

/**
 * The grid state lives in the URL, so a filtered view can be bookmarked, shared and reached
 * again with the browser Back button.
 */
function readFilter(params: URLSearchParams): InstallationFilter {
  const num = (key: string) => {
    const raw = params.get(key);
    return raw ? Number(raw) : null;
  };

  const active = params.get('active');

  return {
    pageNumber: Number(params.get('page')) || 1,
    pageSize: Number(params.get('size')) || 25,
    sortBy: params.get('sort') ?? DEFAULT_SORT,
    sortDirection: params.get('dir') === 'desc' ? 'desc' : 'asc',
    searchTerm: params.get('q') ?? '',
    machineId: num('machine'),
    appNameId: num('app'),
    appStageNameId: num('stage'),
    processorArchitectureId: num('arch'),
    dnsEndpointId: num('dns'),
    rootPathId: num('root'),
    physicalPathId: num('ppath'),
    tagId: num('tag'),
    repositoryId: num('repo'),
    isActive: active === null ? null : active === 'true',
    validFrom: params.get('from'),
    validTo: params.get('to'),
    includeDisabled: params.get('disabled') === 'true',
  };
}

function writeFilter(filter: InstallationFilter): URLSearchParams {
  const params = new URLSearchParams();
  const set = (key: string, value: unknown) => {
    if (value !== null && value !== undefined && value !== '') {
      params.set(key, String(value));
    }
  };

  set('q', filter.searchTerm);
  set('machine', filter.machineId);
  set('app', filter.appNameId);
  set('stage', filter.appStageNameId);
  set('arch', filter.processorArchitectureId);
  set('dns', filter.dnsEndpointId);
  set('root', filter.rootPathId);
  set('ppath', filter.physicalPathId);
  set('tag', filter.tagId);
  set('repo', filter.repositoryId);
  set('from', filter.validFrom);
  set('to', filter.validTo);

  if (filter.isActive !== null && filter.isActive !== undefined) set('active', filter.isActive);
  if (filter.includeDisabled) set('disabled', 'true');
  if (filter.sortBy && filter.sortBy !== DEFAULT_SORT) set('sort', filter.sortBy);
  if (filter.sortDirection === 'desc') set('dir', 'desc');
  if ((filter.pageNumber ?? 1) > 1) set('page', filter.pageNumber);
  if ((filter.pageSize ?? 25) !== 25) set('size', filter.pageSize);

  return params;
}

const SORTABLE: Record<string, string> = {
  machineName: 'Machine',
  appName: 'Application',
  appStageName: 'Stage',
  dnsName: 'DNS',
  rootPath: 'Root path',
  validFromDate: 'Valid',
  isActive: 'Active',
};

export function InstallationsPage() {
  const styles = useStyles();
  const sheet = useSheetStyles();
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useAppToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const { id: routeId } = useParams();

  const { lookups, metadata: lookupMetadata, error: lookupsError, reload: reloadLookups } = useLookups();

  const filter = useMemo(() => readFilter(searchParams), [searchParams]);

  // Ids by default — that is the sheet. The names view is opt-in and lives in the URL like every
  // other piece of grid state, so a link carries the view it was sent in.
  const view = searchParams.get('view') === 'names' ? 'names' : 'ids';
  const isIdView = view === 'ids';
  const columns = isIdView ? ID_COLUMNS : NAME_COLUMNS;

  const [page, setPage] = useState(emptyPage);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<InstallationListItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  // Typing stays local and is written to the URL on a delay, so the address bar (and history)
  // is not rewritten on every keystroke.
  const [searchInput, setSearchInput] = useState(() => searchParams.get('q') ?? '');
  const lastPushedSearch = useRef(searchInput);

  const applyFilter = useCallback(
    (next: InstallationFilter, replace = false) => {
      const params = writeFilter(next);

      // writeFilter rebuilds the query string from the filter alone, which does not know about
      // the view — without this, changing any facet would silently snap the grid back to Ids.
      if (view === 'names') {
        params.set('view', 'names');
      }

      setSearchParams(params, { replace });
    },
    [setSearchParams, view],
  );

  function patchFilter(patch: Partial<InstallationFilter>) {
    // Any filter change resets to page 1 — staying on page 7 of a smaller result is a dead end.
    applyFilter({ ...filter, ...patch, pageNumber: patch.pageNumber ?? 1 });
  }

  const load = useCallback(async (current: InstallationFilter) => {
    setIsLoading(true);
    setError(null);

    try {
      setPage(await api.getInstallations(current));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load installations.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(filter);
  }, [filter, load]);

  // URL → input, so Back and Forward move the search box too.
  useEffect(() => {
    const fromUrl = searchParams.get('q') ?? '';

    if (fromUrl !== lastPushedSearch.current) {
      lastPushedSearch.current = fromUrl;
      setSearchInput(fromUrl);
    }
  }, [searchParams]);

  // Input → URL, debounced.
  useEffect(() => {
    if (searchInput === lastPushedSearch.current) {
      return;
    }

    const handle = setTimeout(() => {
      lastPushedSearch.current = searchInput;
      applyFilter({ ...filter, searchTerm: searchInput, pageNumber: 1 }, true);
    }, 250);

    return () => clearTimeout(handle);
  }, [searchInput, filter, applyFilter]);

  function toggleSort(column: string) {
    patchFilter({
      sortBy: column,
      sortDirection: filter.sortBy === column && filter.sortDirection === 'asc' ? 'desc' : 'asc',
    });
  }

  async function confirmDelete() {
    if (!pendingDelete) {
      return;
    }

    setIsDeleting(true);

    try {
      await api.deleteInstallation(pendingDelete.id);
      toast.success(`Removed ${pendingDelete.appName} on ${pendingDelete.machineName}.`);
      setPendingDelete(null);
      await load(filter);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete the installation.';
      toast.error('Delete failed', message);
      setError(message);
    } finally {
      setIsDeleting(false);
    }
  }

  /**
   * `label` overrides the display text without changing the sort column — the Id view labels its
   * columns as the workbook does (`MachineId`) while still sorting by the readable machine name,
   * which is the only thing `InstallationService.ApplySort` accepts.
   */
  function sortableHeader(column: string, label?: string) {
    const isSorted = filter.sortBy === column;
    const direction = isSorted ? (filter.sortDirection === 'desc' ? 'descending' : 'ascending') : 'none';

    return (
      <TableHeaderCell key={column} aria-sort={direction} className={sheet.headerCell}>
        <button
          type="button"
          className={styles.sortButton}
          onClick={() => toggleSort(column)}
          aria-label={`Sort by ${SORTABLE[column]}`}
        >
          {label ?? SORTABLE[column]}
          {isSorted && <span aria-hidden>{filter.sortDirection === 'desc' ? '↓' : '↑'}</span>}
        </button>
      </TableHeaderCell>
    );
  }

  /**
   * A reference cell in the Id view. The number is the content; the resolved name is the hover
   * text, so "what is 4?" is answerable without leaving the row.
   *
   * A missing optional reference renders as an empty cell rather than a dash — that is what a
   * spreadsheet shows for no value, and both DnsEndpointId and PhysicalPathId are optional by
   * the roadplan.
   */
  function idCell(id: number | null | undefined, name: string | null | undefined) {
    return (
      <TableCell className={sheet.idCell} title={name ?? undefined}>
        {id ?? ''}
      </TableCell>
    );
  }

  function facet(
    label: string,
    placeholder: string,
    items: { id: number; name: string }[],
    selectedId: number | null | undefined,
    onSelect: (id: number | null) => void,
    dropdownClass?: string,
  ) {
    return (
      <Field label={label}>
        <Dropdown
          className={dropdownClass ?? styles.dropdown}
          placeholder={placeholder}
          selectedOptions={selectedId ? [String(selectedId)] : ['']}
          value={items.find((item) => item.id === selectedId)?.name ?? ''}
          onOptionSelect={(_, data) => onSelect(data.optionValue ? Number(data.optionValue) : null)}
        >
          <Option value="">{placeholder}</Option>
          {items.map((item) => (
            <Option key={item.id} value={String(item.id)}>
              {item.name}
            </Option>
          ))}
        </Dropdown>
      </Field>
    );
  }

  const activeValue = filter.isActive === null || filter.isActive === undefined
    ? ''
    : String(filter.isActive);

  function nameOf(kind: Parameters<typeof itemsOf>[1], id: number) {
    return itemsOf(lookups, kind).find((item) => item.id === id)?.name ?? `#${id}`;
  }

  /**
   * One entry per *collapsed* filter that is currently set. These render as dismissable chips
   * next to the disclosure, so a filtered grid always shows why it is filtered even when the
   * control that set it is out of sight. The four facets in the everyday row are excluded —
   * their own dropdowns already display their state.
   */
  const advancedChips: { key: string; label: string; clear: () => void }[] = [];

  if (filter.processorArchitectureId) {
    advancedChips.push({
      key: 'arch',
      label: `Architecture: ${nameOf('processorarchitectures', filter.processorArchitectureId)}`,
      clear: () => patchFilter({ processorArchitectureId: null }),
    });
  }
  if (filter.rootPathId) {
    advancedChips.push({
      key: 'root',
      label: `Root path: ${nameOf('rootpaths', filter.rootPathId)}`,
      clear: () => patchFilter({ rootPathId: null }),
    });
  }
  if (filter.physicalPathId) {
    advancedChips.push({
      key: 'ppath',
      label: `Physical path: ${nameOf('physicalpaths', filter.physicalPathId)}`,
      clear: () => patchFilter({ physicalPathId: null }),
    });
  }
  if (filter.tagId) {
    advancedChips.push({
      key: 'tag',
      label: `Tag: ${nameOf('tags', filter.tagId)}`,
      clear: () => patchFilter({ tagId: null }),
    });
  }
  if (filter.repositoryId) {
    advancedChips.push({
      key: 'repo',
      label: `Repository: ${nameOf('apprepositories', filter.repositoryId)}`,
      clear: () => patchFilter({ repositoryId: null }),
    });
  }
  if (filter.isActive !== null && filter.isActive !== undefined) {
    advancedChips.push({
      key: 'active',
      label: `Serving: ${filter.isActive ? 'Active' : 'Inactive'}`,
      clear: () => patchFilter({ isActive: null }),
    });
  }
  if (filter.validFrom) {
    advancedChips.push({
      key: 'from',
      label: `Installed from: ${filter.validFrom}`,
      clear: () => patchFilter({ validFrom: null }),
    });
  }
  if (filter.validTo) {
    advancedChips.push({
      key: 'to',
      label: `Installed to: ${filter.validTo}`,
      clear: () => patchFilter({ validTo: null }),
    });
  }
  if (filter.includeDisabled) {
    advancedChips.push({
      key: 'disabled',
      label: 'Including decommissioned',
      clear: () => patchFilter({ includeDisabled: false }),
    });
  }

  const dialogMode = routeId === 'new' ? 'create' : routeId ? 'edit' : null;
  const isDetailRoute = location.pathname.endsWith('/view');

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Installations</Title3>
        <Text className={styles.muted}>
          {page.totalCount} record{page.totalCount === 1 ? '' : 's'}
        </Text>
        <div className={styles.spacer} />
        {/* The sheet shows references; people ask questions in names. Both, one click apart. */}
        <Button
          icon={<TextNumberFormatRegular />}
          onClick={() => {
            const params = new URLSearchParams(searchParams);

            if (isIdView) {
              params.set('view', 'names');
            } else {
              params.delete('view');
            }

            setSearchParams(params);
          }}
        >
          {isIdView ? 'Show names' : 'Show Ids'}
        </Button>
        <Button icon={<ArrowClockwiseRegular />} onClick={() => void load(filter)}>
          Refresh
        </Button>
        <Button
          appearance="primary"
          icon={<AddRegular />}
          onClick={() => navigate('/installations/new')}
        >
          New installation
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

      <div className={styles.filterCard}>
        <div className={styles.filterBar}>
          <Field label="Search" className={styles.searchField}>
            <Input
              placeholder="Machine, app, path, DNS, tags..."
              value={searchInput}
              onChange={(_, data) => setSearchInput(data.value)}
            />
          </Field>

          {/* These four carry the roadplan's headline questions — which machines serve
              paha.ga.local, where RC0 of CallCenter runs, what else is on a given machine — so
              they stay one click away. The rest is a click further. */}
          {facet('Machine', 'All', itemsOf(lookups, 'machines'), filter.machineId, (id) =>
            patchFilter({ machineId: id }), styles.barDropdown,
          )}
          {facet('Application', 'All', itemsOf(lookups, 'appnames'), filter.appNameId, (id) =>
            patchFilter({ appNameId: id }), styles.barDropdown,
          )}
          {facet('Stage', 'All', itemsOf(lookups, 'appstagenames'), filter.appStageNameId, (id) =>
            patchFilter({ appStageNameId: id }), styles.barDropdown,
          )}
          {facet('DNS endpoint', 'All', itemsOf(lookups, 'dnsendpoints'), filter.dnsEndpointId, (id) =>
            patchFilter({ dnsEndpointId: id }), styles.barDropdown,
          )}

          <Button
            icon={<FilterRegular />}
            iconPosition="before"
            onClick={() => setShowAdvanced((open) => !open)}
            aria-expanded={showAdvanced}
          >
            More filters{advancedChips.length > 0 ? ` (${advancedChips.length})` : ''}
            {showAdvanced ? <ChevronUpRegular /> : <ChevronDownRegular />}
          </Button>

          <Button
            appearance="subtle"
            // The view is not a filter, so it must not make Clear look actionable on its own.
            disabled={writeFilter(filter).toString() === ''}
            onClick={() => {
              lastPushedSearch.current = '';
              setSearchInput('');

              // Clearing filters is not a request to change the view.
              const kept = new URLSearchParams();
              if (view === 'names') kept.set('view', 'names');

              setSearchParams(kept);
            }}
          >
            Clear
          </Button>
        </div>

        {advancedChips.length > 0 && (
          <div className={styles.chipRow}>
            <TagGroup
              onDismiss={(_, data) => {
                advancedChips.find((chip) => chip.key === data.value)?.clear();
              }}
              aria-label="Active filters"
            >
              {advancedChips.map((chip) => (
                <Tag key={chip.key} value={chip.key} dismissible size="small">
                  {chip.label}
                </Tag>
              ))}
            </TagGroup>
          </div>
        )}

        {showAdvanced && (
          <div className={styles.advancedPanel}>
            <div className={styles.filterGrid}>
              {facet(
                'Architecture',
                'All architectures',
                itemsOf(lookups, 'processorarchitectures'),
                filter.processorArchitectureId,
                (id) => patchFilter({ processorArchitectureId: id }),
              )}
              {facet('Root path', 'All root paths', itemsOf(lookups, 'rootpaths'), filter.rootPathId, (id) =>
                patchFilter({ rootPathId: id }),
              )}
              {facet(
                'Physical path',
                'All physical paths',
                itemsOf(lookups, 'physicalpaths'),
                filter.physicalPathId,
                (id) => patchFilter({ physicalPathId: id }),
              )}
              {facet('Tag', 'All tags', itemsOf(lookups, 'tags'), filter.tagId, (id) =>
                patchFilter({ tagId: id }),
              )}
              {facet('Repository', 'All repositories', itemsOf(lookups, 'apprepositories'), filter.repositoryId, (id) =>
                patchFilter({ repositoryId: id }),
              )}

              <Field label="Serving">
                <Dropdown
                  className={styles.dropdown}
                  placeholder="Any"
                  selectedOptions={[activeValue]}
                  value={activeValue === '' ? 'Any' : activeValue === 'true' ? 'Active' : 'Inactive'}
                  onOptionSelect={(_, data) =>
                    patchFilter({ isActive: data.optionValue === '' ? null : data.optionValue === 'true' })
                  }
                >
                  <Option value="">Any</Option>
                  <Option value="true">Active</Option>
                  <Option value="false">Inactive</Option>
                </Dropdown>
              </Field>

              {/* Matches on overlap, so an installation spanning the window counts even if it
                  started earlier. */}
              <Field label="Installed from">
                <Input
                  type="date"
                  value={filter.validFrom ?? ''}
                  onChange={(_, data) => patchFilter({ validFrom: data.value || null })}
                />
              </Field>

              <Field label="Installed to">
                <Input
                  type="date"
                  value={filter.validTo ?? ''}
                  onChange={(_, data) => patchFilter({ validTo: data.value || null })}
                />
              </Field>
            </div>

            <div className={styles.filterFooter}>
              {/* Decommissioned rows are soft-deleted, so a past-date query cannot see them
                  without this. Off by default to keep the everyday grid clean. */}
              <Checkbox
                label="Include decommissioned"
                checked={filter.includeDisabled ?? false}
                onChange={(_, data) => patchFilter({ includeDisabled: Boolean(data.checked) })}
              />
            </div>
          </div>
        )}
      </div>

      <div className={styles.tableWrapper}>
        {isLoading && (
          <div className={styles.loadingBar}>
            <Spinner size="tiny" label="Loading..." />
          </div>
        )}

        <Table
          size="small"
          style={{ minWidth: `${widthOf(columns)}px` }}
          className={mergeClasses(sheet.table, isLoading ? styles.dimmed : undefined)}
        >
          <colgroup>
            {columns.map((column) => (
              <col key={column.key} style={{ width: `${column.width}px` }} />
            ))}
          </colgroup>

          <TableHeader>
            <TableRow>
              {/* The gutter's corner cell, as on a sheet: no label, it numbers the rows. */}
              <TableHeaderCell className={sheet.headerCell} aria-label="Row number" />
              <TableHeaderCell className={sheet.headerCell}>ID</TableHeaderCell>

              {isIdView ? (
                <>
                  {/* Header names are the workbook's, not prettier versions of them — this screen
                      is the ApplicationInstalation sheet and should be recognisable as it. */}
                  {sortableHeader('machineName', 'MachineId')}
                  {sortableHeader('appName', 'AppNameId')}
                  {sortableHeader('appStageName', 'AppStageNameId')}
                  <TableHeaderCell className={sheet.headerCell}>ProcessorArchitectureId</TableHeaderCell>
                  {sortableHeader('dnsName', 'DnsEndpointId')}
                  {sortableHeader('rootPath', 'RootPathId')}
                  <TableHeaderCell className={sheet.headerCell}>PhysicalPathId</TableHeaderCell>
                </>
              ) : (
                <>
                  {sortableHeader('machineName')}
                  {sortableHeader('appName')}
                  {sortableHeader('appStageName')}
                  <TableHeaderCell className={sheet.headerCell}>Arch</TableHeaderCell>
                  {sortableHeader('dnsName')}
                  {sortableHeader('rootPath')}
                  <TableHeaderCell className={sheet.headerCell}>Physical path</TableHeaderCell>
                </>
              )}

              <TableHeaderCell className={sheet.headerCell}>Tags</TableHeaderCell>
              {sortableHeader('validFromDate')}
              {sortableHeader('isActive')}
              <TableHeaderCell className={sheet.headerCell}>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>

          <TableBody>
            {page.items.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={columns.length}>
                  <span className={styles.muted}>No installations match the current filter.</span>
                </TableCell>
              </TableRow>
            )}

            {page.items.map((item, index) => (
              <TableRow key={item.id}>
                <TableCell className={sheet.gutterCell}>
                  {((page.pageNumber || 1) - 1) * (page.pageSize || 25) + index + 1}
                </TableCell>
                <TableCell className={sheet.idCell}>{item.id}</TableCell>

                {isIdView ? (
                  <>
                    {idCell(item.machineId, item.machineName)}
                    {idCell(item.appNameId, item.appName)}
                    {idCell(item.appStageNameId, item.appStageName)}
                    {idCell(item.processorArchitectureId, item.processorArchitecture)}
                    {idCell(item.dnsEndpointId, item.dnsName)}
                    {idCell(item.rootPathId, item.rootPath)}
                    {idCell(item.physicalPathId, item.physicalPath)}
                  </>
                ) : (
                  <>
                    <TableCell className={styles.truncate} title={item.machineName}>
                      {item.machineName}
                    </TableCell>
                    <TableCell className={styles.truncate} title={item.appName}>
                      {item.appName}
                    </TableCell>
                    <TableCell className={styles.truncate} title={item.appStageName}>
                      {item.appStageName}
                    </TableCell>
                    <TableCell>{item.processorArchitecture}</TableCell>
                    <TableCell className={styles.truncate} title={item.dnsName ?? undefined}>
                      {item.dnsName ?? <span className={styles.muted}>—</span>}
                    </TableCell>
                    <TableCell
                      className={mergeClasses(styles.mono, styles.truncate)}
                      title={item.rootPath}
                    >
                      {item.rootPath}
                    </TableCell>
                    <TableCell
                      className={mergeClasses(styles.mono, styles.truncate)}
                      title={item.physicalPath ?? undefined}
                    >
                      {item.physicalPath ?? <span className={styles.muted}>—</span>}
                    </TableCell>
                  </>
                )}
                <TableCell title={item.tags.join(', ')}>
                  {item.tags.length === 0 ? (
                    <span className={styles.muted}>—</span>
                  ) : (
                    <div className={styles.tagList}>
                      {item.tags.map((tag) => (
                        <Badge key={tag} appearance="tint" color="informative" size="small">
                          {tag}
                        </Badge>
                      ))}
                    </div>
                  )}
                </TableCell>
                <TableCell className={styles.nowrap}>
                  {item.validFromDate} →{' '}
                  {item.validToDate ?? <span className={styles.muted}>open</span>}
                </TableCell>
                <TableCell>
                  <Badge appearance="filled" color={item.isActive ? 'success' : 'informative'}>
                    {item.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className={styles.rowActions}>
                    <Tooltip content="View details" relationship="label">
                      <Button
                        size="small"
                        appearance="subtle"
                        icon={<EyeRegular />}
                        onClick={() => navigate(`/installations/${item.id}/view`)}
                      />
                    </Tooltip>
                    <Tooltip content="Edit" relationship="label">
                      <Button
                        size="small"
                        appearance="subtle"
                        icon={<EditRegular />}
                        onClick={() => navigate(`/installations/${item.id}`)}
                      />
                    </Tooltip>
                    <Tooltip content="Decommission" relationship="label">
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

      <div className={styles.footer}>
        <span className={styles.muted}>
          Page {page.pageNumber} of {Math.max(page.totalPages, 1)}
        </span>
        <div className={styles.spacer} />
        <Button
          disabled={page.pageNumber <= 1}
          onClick={() => patchFilter({ pageNumber: (filter.pageNumber ?? 1) - 1 })}
        >
          Previous
        </Button>
        <Button
          disabled={page.pageNumber >= page.totalPages}
          onClick={() => patchFilter({ pageNumber: (filter.pageNumber ?? 1) + 1 })}
        >
          Next
        </Button>
      </div>

      {isDetailRoute && routeId && routeId !== 'new' && (
        <InstallationDetailDialog
          installationId={Number(routeId)}
          onClose={() => navigate({ pathname: '/installations', search: searchParams.toString() })}
          onEdit={() => navigate(`/installations/${routeId}`)}
        />
      )}

      {!isDetailRoute && dialogMode && (
        <InstallationDialog
          installationId={dialogMode === 'create' ? null : Number(routeId)}
          lookups={lookups}
          lookupMetadata={lookupMetadata}
          onClose={() => navigate({ pathname: '/installations', search: searchParams.toString() })}
          onSaved={() => {
            toast.success(dialogMode === 'create' ? 'Installation created.' : 'Installation saved.');
            navigate({ pathname: '/installations', search: searchParams.toString() });
            void load(filter);
            void reloadLookups();
          }}
        />
      )}

      {pendingDelete && (
        <ConfirmDialog
          title="Decommission installation"
          message={`Remove the installation of ${pendingDelete.appName} (${pendingDelete.appStageName}) on ${pendingDelete.machineName}? The record is kept and can be shown again with "Include decommissioned".`}
          confirmLabel="Decommission"
          isBusy={isDeleting}
          onConfirm={() => void confirmDelete()}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  );
}
