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
} from '@fluentui/react-icons';
import { api } from '../api/client';
import type { DataViewOutput, InstallationFilter, InstallationListItem } from '../api/types';
import { itemsOf, useLookups } from '../hooks/useLookups';
import { useAppToast } from '../hooks/useAppToast';
import { InstallationDialog } from '../components/InstallationDialog';
import { InstallationDetailDrawer } from '../components/InstallationDetailDrawer';
import { LookupPickerDrawer } from '../components/LookupPickerDrawer';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useSheetStyles } from '../styles/sheetStyles';

/**
 * The grid shows resolved values, never references.
 *
 * The fact table stores foreign keys — that is the roadplan's normalisation rule and it is right
 * — but a key is not an answer. The success criteria are questions in words ("which machines
 * serve paha.ga.local?"), and `2` does not answer one, so every reference is displayed as the
 * lookup value it points at. The Id itself is not shown either: nobody operating a server asks
 * for installation 14, they ask for RC0 of CallCenter on GAIIS2.
 *
 * Fluent's Table is `table-layout: fixed`, so these widths are the only thing setting column
 * size. Sized against the real seed values at 1600px — the longest of each (Data Exchange WebApi,
 * https://vipsprava.1220.cz, c:\inetpub\callcenter.rc0) fits without truncating, and the total
 * still leaves Active and Actions on screen rather than off the right edge.
 */
const COLUMNS = [
  // The row-number gutter, as on a spreadsheet: position in the result, not the record's Id.
  { key: 'rowNumber', width: 44 },
  { key: 'machine', width: 135 },
  { key: 'application', width: 175 },
  { key: 'stage', width: 90 },
  { key: 'arch', width: 60 },
  { key: 'dns', width: 195 },
  { key: 'rootPath', width: 150 },
  { key: 'physicalPath', width: 195 },
  { key: 'tags', width: 150 },
  { key: 'valid', width: 145 },
  { key: 'active', width: 85 },
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
  dropdown: { minWidth: 0 },
  // A facet in the everyday row must not stretch to fill the leftover space; the search box does.
  barFacet: { width: '160px' },
  // Shaped like the Dropdown it replaced — value on the left, chevron on the right — so the row
  // still reads as a row of form controls rather than a row of buttons.
  facetButton: {
    width: '100%',
    justifyContent: 'space-between',
    fontWeight: tokens.fontWeightRegular,
  },
  facetValue: { overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
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

  // The row is the control that opens the detail, so it has to look like one and take focus
  // like one. No colour of its own — the hover fill is the sheet's, already defined.
  selectableRow: {
    cursor: 'pointer',
    ':focus-visible': { outline: `2px solid ${tokens.colorStrokeFocus2}`, outlineOffset: '-2px' },
  },
  // Which row the open drawer belongs to. A left marker rather than a fill, because the sheet
  // already uses fills for the header and gutter and a third one would read as another band.
  selectedRow: {
    '& td': { backgroundColor: tokens.colorNeutralBackground1Selected },
    '& td:first-child': { boxShadow: `inset 2px 0 0 0 ${tokens.colorBrandStroke1}` },
  },

  // Tags are a set, not a sentence — badges wrap inside the cell rather than running past it.
  tagList: { display: 'flex', flexWrap: 'wrap', gap: '4px', minWidth: 0 },
  // A tag name can be longer than the column ("incoming-postal-web"). Without a cap the badge
  // keeps its one-line height while the text inside wraps, so the words spill out of the pill and
  // across the rows above and below. Clipped to the column width instead; the cell's title
  // attribute still carries the full list.
  tagBadge: { maxWidth: '100%', minWidth: 0 },
  tagText: { display: 'block', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
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

const DEFAULT_SORT = 'machineName';

/**
 * Rows per page. A sheet is read by scanning it, so the page is sized to be scrolled rather than
 * paged through — 75 covers most of a real inventory in one or two pages.
 *
 * Declared above `emptyPage`, which reads it while this module is still being evaluated.
 */
const DEFAULT_PAGE_SIZE = 75;

const emptyPage: DataViewOutput<InstallationListItem> = {
  items: [],
  totalCount: 0,
  pageNumber: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  totalPages: 0,
};

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
    pageSize: Number(params.get('size')) || DEFAULT_PAGE_SIZE,
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
    // Several tags share one parameter, comma-separated: "tag=2,5". A repeated key would work
    // too, but one readable value keeps a shared link legible.
    tagIds: (params.get('tag') ?? '')
      .split(',')
      .map((value) => Number(value))
      .filter((value) => Number.isFinite(value) && value > 0),
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
  set('tag', filter.tagIds?.length ? filter.tagIds.join(',') : null);
  set('repo', filter.repositoryId);
  set('from', filter.validFrom);
  set('to', filter.validTo);

  if (filter.isActive !== null && filter.isActive !== undefined) set('active', filter.isActive);
  if (filter.includeDisabled) set('disabled', 'true');
  if (filter.sortBy && filter.sortBy !== DEFAULT_SORT) set('sort', filter.sortBy);
  if (filter.sortDirection === 'desc') set('dir', 'desc');
  if ((filter.pageNumber ?? 1) > 1) set('page', filter.pageNumber);
  if ((filter.pageSize ?? DEFAULT_PAGE_SIZE) !== DEFAULT_PAGE_SIZE) set('size', filter.pageSize);

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

  const [page, setPage] = useState(emptyPage);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<InstallationListItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  /**
   * Which lookup the picker drawer is open on, or null. The whole descriptor is kept rather than
   * a key: the drawer needs the label, the current value and where to put the answer, and
   * rebuilding that from a key would mean a second switch over the same list of facets.
   */
  const [picker, setPicker] = useState<{
    label: string;
    clearLabel: string;
    kind: Parameters<typeof itemsOf>[1];
    selected: number[];
    multiple: boolean;
    onApply: (ids: number[]) => void;
  } | null>(null);

  // Typing stays local and is written to the URL on a delay, so the address bar (and history)
  // is not rewritten on every keystroke.
  const [searchInput, setSearchInput] = useState(() => searchParams.get('q') ?? '');
  const lastPushedSearch = useRef(searchInput);

  const applyFilter = useCallback(
    (next: InstallationFilter, replace = false) => {
      setSearchParams(writeFilter(next), { replace });
    },
    [setSearchParams],
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
   * A facet is a field that opens the value picker, not a dropdown menu.
   *
   * It still reads as a form control — label above, current value in the box, chevron on the
   * right — but the values themselves are chosen in a drawer, which can hold a search box and a
   * few hundred rows without any of it hanging off the edge of the window.
   */
  function facet(
    label: string,
    clearLabel: string,
    kind: Parameters<typeof itemsOf>[1],
    selectedId: number | null | undefined,
    onSelect: (id: number | null) => void,
    fieldClass?: string,
  ) {
    return pickerField(
      label,
      clearLabel,
      kind,
      selectedId ? [selectedId] : [],
      false,
      (ids) => onSelect(ids[0] ?? null),
      fieldClass,
    );
  }

  /** The same field, for the one facet that takes several values at once. */
  function multiFacet(
    label: string,
    clearLabel: string,
    kind: Parameters<typeof itemsOf>[1],
    selected: number[],
    onApply: (ids: number[]) => void,
    fieldClass?: string,
  ) {
    return pickerField(label, clearLabel, kind, selected, true, onApply, fieldClass);
  }

  function pickerField(
    label: string,
    clearLabel: string,
    kind: Parameters<typeof itemsOf>[1],
    selected: number[],
    multiple: boolean,
    onApply: (ids: number[]) => void,
    fieldClass?: string,
  ) {
    // Two or more values are summarised rather than listed: the field is 160px wide and the
    // chips under the bar already spell the selection out in full.
    const shown =
      selected.length === 0
        ? null
        : selected.length === 1
          ? nameOf(kind, selected[0])
          : `${selected.length} selected`;

    return (
      <Field label={label} className={fieldClass}>
        <Button
          className={styles.facetButton}
          onClick={() => setPicker({ label, clearLabel, kind, selected, multiple, onApply })}
          aria-haspopup="dialog"
        >
          <span className={mergeClasses(styles.facetValue, shown ? undefined : styles.muted)}>
            {shown ?? 'All'}
          </span>
          <ChevronDownRegular />
        </Button>
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
   * One entry per filter that is currently set. These render as dismissable chips beside the
   * Filters button, so a filtered grid always says why it is filtered — with every control now
   * inside a drawer, this row is the only thing standing between a filtered view and a reader
   * who assumes they are looking at everything.
   */
  const activeChips: { key: string; label: string; clear: () => void }[] = [];

  if (filter.searchTerm) {
    activeChips.push({
      key: 'search',
      label: `Search: ${filter.searchTerm}`,
      clear: () => {
        lastPushedSearch.current = '';
        setSearchInput('');
        patchFilter({ searchTerm: '' });
      },
    });
  }
  if (filter.machineId) {
    activeChips.push({
      key: 'machine',
      label: `Machine: ${nameOf('machines', filter.machineId)}`,
      clear: () => patchFilter({ machineId: null }),
    });
  }
  if (filter.appNameId) {
    activeChips.push({
      key: 'app',
      label: `Application: ${nameOf('appnames', filter.appNameId)}`,
      clear: () => patchFilter({ appNameId: null }),
    });
  }
  if (filter.appStageNameId) {
    activeChips.push({
      key: 'stage',
      label: `Stage: ${nameOf('appstagenames', filter.appStageNameId)}`,
      clear: () => patchFilter({ appStageNameId: null }),
    });
  }
  if (filter.dnsEndpointId) {
    activeChips.push({
      key: 'dns',
      label: `DNS endpoint: ${nameOf('dnsendpoints', filter.dnsEndpointId)}`,
      clear: () => patchFilter({ dnsEndpointId: null }),
    });
  }
  if (filter.processorArchitectureId) {
    activeChips.push({
      key: 'arch',
      label: `Architecture: ${nameOf('processorarchitectures', filter.processorArchitectureId)}`,
      clear: () => patchFilter({ processorArchitectureId: null }),
    });
  }
  if (filter.rootPathId) {
    activeChips.push({
      key: 'root',
      label: `Root path: ${nameOf('rootpaths', filter.rootPathId)}`,
      clear: () => patchFilter({ rootPathId: null }),
    });
  }
  if (filter.physicalPathId) {
    activeChips.push({
      key: 'ppath',
      label: `Physical path: ${nameOf('physicalpaths', filter.physicalPathId)}`,
      clear: () => patchFilter({ physicalPathId: null }),
    });
  }
  if (filter.tagIds?.length) {
    activeChips.push({
      key: 'tag',
      label:
        filter.tagIds.length === 1
          ? `Tag: ${nameOf('tags', filter.tagIds[0])}`
          : `Tags: ${filter.tagIds.map((id) => nameOf('tags', id)).join(', ')}`,
      clear: () => patchFilter({ tagIds: [] }),
    });
  }
  if (filter.repositoryId) {
    activeChips.push({
      key: 'repo',
      label: `Repository: ${nameOf('apprepositories', filter.repositoryId)}`,
      clear: () => patchFilter({ repositoryId: null }),
    });
  }
  if (filter.isActive !== null && filter.isActive !== undefined) {
    activeChips.push({
      key: 'active',
      label: `Serving: ${filter.isActive ? 'Active' : 'Inactive'}`,
      clear: () => patchFilter({ isActive: null }),
    });
  }
  if (filter.validFrom) {
    activeChips.push({
      key: 'from',
      label: `Installed from: ${filter.validFrom}`,
      clear: () => patchFilter({ validFrom: null }),
    });
  }
  if (filter.validTo) {
    activeChips.push({
      key: 'to',
      label: `Installed to: ${filter.validTo}`,
      clear: () => patchFilter({ validTo: null }),
    });
  }
  if (filter.includeDisabled) {
    activeChips.push({
      key: 'disabled',
      label: 'Including decommissioned',
      clear: () => patchFilter({ includeDisabled: false }),
    });
  }

  /** What the disclosure is hiding: the chips for the four facets in the bar are not counted. */
  const advancedCount = activeChips.filter(
    (chip) => !['search', 'machine', 'app', 'stage', 'dns'].includes(chip.key),
  ).length;

  function clearFilters() {
    lastPushedSearch.current = '';
    setSearchInput('');
    setSearchParams(new URLSearchParams());
  }

  const dialogMode = routeId === 'new' ? 'create' : routeId ? 'edit' : null;
  const isDetailRoute = location.pathname.endsWith('/view');

  /**
   * The selection is the URL, not component state. A drawer that outlives a Back press, or that
   * cannot be linked to, would be a different thing from every other piece of grid state here —
   * and the filters already work this way.
   */
  const selectedId = isDetailRoute && routeId && routeId !== 'new' ? Number(routeId) : null;

  function openDetail(id: number) {
    navigate({ pathname: `/installations/${id}/view`, search: searchParams.toString() });
  }

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Installations</Title3>
        <Text className={styles.muted}>
          {page.totalCount} record{page.totalCount === 1 ? '' : 's'}
        </Text>
        <div className={styles.spacer} />
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
          {facet('Machine', 'All machines', 'machines', filter.machineId, (id) =>
            patchFilter({ machineId: id }), styles.barFacet,
          )}
          {facet('Application', 'All applications', 'appnames', filter.appNameId, (id) =>
            patchFilter({ appNameId: id }), styles.barFacet,
          )}
          {facet('Stage', 'All stages', 'appstagenames', filter.appStageNameId, (id) =>
            patchFilter({ appStageNameId: id }), styles.barFacet,
          )}
          {facet('DNS endpoint', 'All endpoints', 'dnsendpoints', filter.dnsEndpointId, (id) =>
            patchFilter({ dnsEndpointId: id }), styles.barFacet,
          )}

          <Button
            icon={<FilterRegular />}
            iconPosition="before"
            onClick={() => setShowAdvanced((open) => !open)}
            aria-expanded={showAdvanced}
          >
            More filters{advancedCount > 0 ? ` (${advancedCount})` : ''}
            {showAdvanced ? <ChevronUpRegular /> : <ChevronDownRegular />}
          </Button>

          {/* Named for its result, not its mechanism: the button is pressed to see everything
              again, and "Show all" says that where "Clear" only described what it did to the
              controls. */}
          <Button
            appearance="subtle"
            disabled={writeFilter(filter).toString() === ''}
            onClick={clearFilters}
          >
            Show all
          </Button>
        </div>

        {activeChips.length > 0 && (
          <div className={styles.chipRow}>
            <TagGroup
              onDismiss={(_, data) => {
                activeChips.find((chip) => chip.key === data.value)?.clear();
              }}
              aria-label="Active filters"
            >
              {activeChips.map((chip) => (
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
                'processorarchitectures',
                filter.processorArchitectureId,
                (id) => patchFilter({ processorArchitectureId: id }),
              )}
              {facet('Root path', 'All root paths', 'rootpaths', filter.rootPathId, (id) =>
                patchFilter({ rootPathId: id }),
              )}
              {facet(
                'Physical path',
                'All physical paths',
                'physicalpaths',
                filter.physicalPathId,
                (id) => patchFilter({ physicalPathId: id }),
              )}
              {/* The one multi-select facet: asking about "web or service" is the normal tag
                  question, where one machine or one stage is not. */}
              {multiFacet('Tags', 'All tags', 'tags', filter.tagIds ?? [], (ids) =>
                patchFilter({ tagIds: ids }),
              )}
              {facet('Repository', 'All repositories', 'apprepositories', filter.repositoryId, (id) =>
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

      {/* Values are chosen in a drawer, not in a dropdown menu: a lookup here runs to hundreds of
          rows, which is more than a menu can show without becoming a scrolling column pinned to a
          control near the edge of the window. */}
      {picker && (
        <LookupPickerDrawer
          label={picker.label}
          clearLabel={picker.clearLabel}
          items={itemsOf(lookups, picker.kind)}
          selected={picker.selected}
          multiple={picker.multiple}
          onApply={picker.onApply}
          onClose={() => setPicker(null)}
        />
      )}

      <div className={styles.tableWrapper}>
        {isLoading && (
          <div className={styles.loadingBar}>
            <Spinner size="tiny" label="Loading..." />
          </div>
        )}

        {/* `width: 100%` with the sum as the minimum: the columns below are a floor, not a fixed
            size, so a window wider than the grid spreads the slack across the columns instead of
            leaving a gap — and the sideways scrollbar appears only when the window really is too
            narrow to hold the twelve columns. */}
        <Table
          size="small"
          style={{ width: '100%', minWidth: `${widthOf(COLUMNS)}px` }}
          className={mergeClasses(sheet.table, isLoading ? styles.dimmed : undefined)}
        >
          <colgroup>
            {COLUMNS.map((column) => (
              <col key={column.key} style={{ width: `${column.width}px` }} />
            ))}
          </colgroup>

          <TableHeader>
            <TableRow>
              {/* The gutter's corner cell, as on a sheet: no label, it numbers the rows. */}
              <TableHeaderCell className={sheet.headerCell} aria-label="Row number" />

              {sortableHeader('machineName')}
              {sortableHeader('appName')}
              {sortableHeader('appStageName')}
              <TableHeaderCell className={sheet.headerCell}>Arch</TableHeaderCell>
              {sortableHeader('dnsName')}
              {sortableHeader('rootPath')}
              <TableHeaderCell className={sheet.headerCell}>Physical path</TableHeaderCell>

              <TableHeaderCell className={sheet.headerCell}>Tags</TableHeaderCell>
              {sortableHeader('validFromDate')}
              {sortableHeader('isActive')}
              <TableHeaderCell className={sheet.headerCell}>Actions</TableHeaderCell>
            </TableRow>
          </TableHeader>

          <TableBody>
            {page.items.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={COLUMNS.length}>
                  <span className={styles.muted}>No installations match the current filter.</span>
                </TableCell>
              </TableRow>
            )}

            {page.items.map((item, index) => (
              // Selecting a row is how the detail is opened, so the whole row is the target —
              // not a 24px icon at the far right of a 1500px line. The icon stays for anyone
              // who reaches for it, and for keyboard users the row is a real tab stop.
              <TableRow
                key={item.id}
                className={mergeClasses(
                  styles.selectableRow,
                  selectedId === item.id ? styles.selectedRow : undefined,
                )}
                tabIndex={0}
                aria-selected={selectedId === item.id}
                onClick={() => openDetail(item.id)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    openDetail(item.id);
                  }
                }}
              >
                <TableCell className={sheet.gutterCell}>
                  {((page.pageNumber || 1) - 1) * (page.pageSize || DEFAULT_PAGE_SIZE) + index + 1}
                </TableCell>

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
                <TableCell title={item.tags.join(', ')}>
                  {item.tags.length === 0 ? (
                    <span className={styles.muted}>—</span>
                  ) : (
                    <div className={styles.tagList}>
                      {item.tags.map((tag) => (
                        <Badge
                          key={tag}
                          appearance="tint"
                          color="informative"
                          size="small"
                          className={styles.tagBadge}
                        >
                          <span className={styles.tagText}>{tag}</span>
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
                {/* Each of these does something other than "select this row", so none of them
                    may also trigger the row's own click. */}
                <TableCell onClick={(event) => event.stopPropagation()}>
                  <div className={styles.rowActions}>
                    <Tooltip content="View details" relationship="label">
                      <Button
                        size="small"
                        appearance="subtle"
                        icon={<EyeRegular />}
                        onClick={() => openDetail(item.id)}
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

      {selectedId !== null && (
        <InstallationDetailDrawer
          installationId={selectedId}
          onClose={() => navigate({ pathname: '/installations', search: searchParams.toString() })}
          onEdit={() => navigate(`/installations/${selectedId}`)}
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
