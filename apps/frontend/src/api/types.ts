/**
 * Mirrors the DTOs in `apps/netcorebackends/Argus.Api/WebApiPoco/`.
 * Hand-written for now; the controllers all carry `[EndpointName]`, so this file can be
 * replaced by a generated client later without touching the backend.
 */

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message?: string | null;
}

export interface ErrorResponse {
  success: boolean;
  errorCode: string;
  message: string;
  traceId?: string | null;
}

export interface DataViewOutput<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface LookupItem {
  id: number;
  name: string;
  description?: string | null;
  /** Only meaningful for appstagenames. Must be round-tripped on edit or ordering is lost. */
  sortOrder: number;
  /** Only meaningful for dnsendpoints. Must be round-tripped on edit or the flag is lost. */
  isLoadBalancer: boolean;
}

export interface LookupUpsert {
  name: string;
  description?: string | null;
  sortOrder: number;
  isLoadBalancer: boolean;
}

/**
 * Matches the `LookupKind` enum on the server (route is case-insensitive).
 *
 * The list stays here — and only here — because the installation form reaches for specific kinds
 * by name (`lookups.machines`), and a type cannot be derived from a runtime endpoint. Everything
 * *about* a kind (label, name length, which fields it has, whether it is writable) comes from
 * `GET /api/lookups` instead of being written down a second time.
 *
 * `apprepositories` is readable here — a dropdown of repositories is the same query as any other
 * kind — but writes go through `/api/apprepositories`, because `LookupUpsert` has nowhere to put
 * the repository type or the installation links and would erase both.
 */
export type LookupKind =
  | 'machines'
  | 'appnames'
  | 'appstagenames'
  | 'processorarchitectures'
  | 'dnsendpoints'
  | 'rootpaths'
  | 'physicalpaths'
  | 'tags'
  | 'repositorytypes'
  | 'apprepositories';

/**
 * A grid row. Carries both halves of every reference — the foreign key and the name it resolves
 * to — because the grid shows the Ids by default (the roadplan's fact table is references only)
 * and the names on hover or in the names view.
 */
export interface InstallationListItem {
  id: number;
  machineId: number;
  machineName: string;
  appNameId: number;
  appName: string;
  appStageNameId: number;
  appStageName: string;
  processorArchitectureId: number;
  processorArchitecture: string;
  /** Null for a service or console app, which has no public address. */
  dnsEndpointId?: number | null;
  dnsName?: string | null;
  rootPathId: number;
  rootPath: string;
  physicalPathId?: number | null;
  physicalPath?: string | null;
  /** Resolved tag names, sorted. One badge per entry. */
  tags: string[];
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
}

export interface AppRepository {
  id: number;
  repositoryUrl: string;
  /** Id into the repositorytypes lookup; null when it was never recorded. */
  repositoryTypeId?: number | null;
  /** Display name for the id above, so a grid renders without a second request. */
  repositoryTypeName?: string | null;
  description?: string | null;
  /** Installations built from this repository. Many-to-many, not an owning application. */
  installationIds: number[];
}

export interface AppRepositoryUpsert {
  repositoryUrl: string;
  repositoryTypeId?: number | null;
  description?: string | null;
  /** An empty list leaves the repository registered but unattached. */
  installationIds: number[];
}

export interface InstallationDetail {
  id: number;
  machineId: number;
  machineName: string;
  appNameId: number;
  appName: string;
  appStageNameId: number;
  appStageName: string;
  processorArchitectureId: number;
  processorArchitecture: string;
  dnsEndpointId?: number | null;
  dnsName?: string | null;
  rootPathId: number;
  rootPath: string;
  physicalPathId?: number | null;
  physicalPath?: string | null;
  /** Linked tags, Id + name. The edit form submits the Ids. */
  tags: LookupItem[];
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
  createdUtc: string;
  modifiedUtc?: string | null;
  appRepositories: AppRepository[];
}

/**
 * Ids only — no names. A client cannot invent a machine or a path here; it must exist in its
 * lookup first. That is what makes the installation the last table to be filled.
 */
export interface InstallationUpsert {
  machineId: number;
  appNameId: number;
  appStageNameId: number;
  processorArchitectureId: number;
  dnsEndpointId?: number | null;
  rootPathId: number;
  physicalPathId?: number | null;
  /** Tags to link. An empty list clears them. Duplicates are ignored server-side. */
  tagIds: number[];
  /** Repositories to link. Same rules as `tagIds`. */
  repositoryIds: number[];
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
}

/**
 * Grid filter. Every key here must match a property on `InstallationFilterDto` exactly —
 * ASP.NET binds by name and silently ignores an unknown one, so a typo becomes a facet that
 * looks like it works and quietly returns everything.
 */
export interface InstallationFilter {
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  searchTerm?: string;
  machineId?: number | null;
  appNameId?: number | null;
  appStageNameId?: number | null;
  processorArchitectureId?: number | null;
  dnsEndpointId?: number | null;
  rootPathId?: number | null;
  physicalPathId?: number | null;
  /** The one multi-value facet. Matched with OR: any of these tags is a hit. */
  tagIds?: number[];
  /** Single repository, for the same reason as `tagId`. */
  repositoryId?: number | null;
  isActive?: boolean | null;
  /** Start of the period of interest (YYYY-MM-DD). Matches on overlap, not containment. */
  validFrom?: string | null;
  /** End of the period of interest (YYYY-MM-DD). */
  validTo?: string | null;
  /** Include soft-deleted rows — required to see decommissioned installations. */
  includeDisabled?: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  expiresUtc: string;
  username: string;
  displayName: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  displayName: string;
}

/**
 * Describes one lookup kind, straight from `GET /api/lookups`. The Lookups screen builds its tabs
 * and form fields from these rather than from a list kept here, so a kind added on the server
 * appears in the UI with no frontend change.
 */
export interface LookupMetadata {
  kind: LookupKind;
  label: string;
  singular: string;
  isReadOnly: boolean;
  maxNameLength: number;
  hasDescription: boolean;
  hasSortOrder: boolean;
  hasLoadBalancer: boolean;
}

/**
 * An account that can sign in. Mirrors `UserDto`, which carries no hash and no salt — the only
 * direction a password ever travels is in, through `setUserPassword`.
 */
export interface User {
  id: number;
  username: string;
  displayName: string;
  isEnabled: boolean;
  createdUtc: string;
  lastLoginUtc: string | null;
}

export interface UserUpsert {
  username: string;
  displayName: string;
  /** Required when creating. Ignored by the server on update — see `12_user_management.md`. */
  password?: string;
}

/** One file in the server's log directory, from `POST /api/logs/search`. */
export interface LogFile {
  name: string;
  /** "action" is the audit trail, "diagnostic" is everything log4net's root logger writes. */
  kind: 'action' | 'diagnostic';
  sizeBytes: number;
  lastWriteUtc: string;
}

/** The tail of one log file, oldest line first. */
export interface LogContent {
  name: string;
  lines: string[];
  totalLines: number;
  /** True when older lines were left out because the line limit was reached. */
  isTruncated: boolean;
  lastWriteUtc: string;
}

/**
 * One row of an installation's history — `POST /api/installations/{id}/journal`.
 *
 * Values are text as they were resolved when the change was made, so a lookup renamed later does
 * not rewrite what the history says. `oldValueId` / `newValueId` keep the raw reference alongside.
 */
export interface JournalEntry {
  id: number;
  /** Rows sharing this were written by one save — one edit by one person. */
  changeSetId: string;
  changedUtc: string;
  changedBy: string;
  action: 'Created' | 'Updated' | 'Deleted' | 'Restored' | 'LinkAdded' | 'LinkRemoved';
  entityName: string;
  field: string | null;
  oldValue: string | null;
  newValue: string | null;
  oldValueId: number | null;
  newValueId: number | null;
}
