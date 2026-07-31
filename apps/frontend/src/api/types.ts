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
 * All nine shared values from the roadplan. `apprepositories` is readable here — a dropdown
 * of repositories is the same query as any other kind — but writes go through
 * `/api/apprepositories`, because `LookupUpsert` has nowhere to put the repository type or
 * the installation links and would erase both.
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
  | 'apprepositories';

/** The eight kinds the Lookups screen may create, edit and delete. */
export const editableLookupKinds = [
  'machines',
  'appnames',
  'appstagenames',
  'processorarchitectures',
  'dnsendpoints',
  'rootpaths',
  'physicalpaths',
  'tags',
] as const satisfies readonly LookupKind[];

export type EditableLookupKind = (typeof editableLookupKinds)[number];

/**
 * Mirrors the `HasMaxLength` on each entity's `Name` in
 * `Database/Entities/Configurations/`, which `LookupHandler.MaxNameLength()` reads out of the
 * EF model at runtime. Kept in sync by hand so the form stops an over-long name before the
 * round trip; the server check remains the real one.
 */
export const lookupMaxNameLength: Record<LookupKind, number> = {
  machines: 128,
  appnames: 128,
  appstagenames: 64,
  processorarchitectures: 32,
  dnsendpoints: 256,
  rootpaths: 256,
  physicalpaths: 512,
  tags: 64,
  apprepositories: 512,
};

export interface InstallationListItem {
  id: number;
  machineName: string;
  appName: string;
  appStageName: string;
  processorArchitecture: string;
  dnsName?: string | null;
  rootPath: string;
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
  repositoryType: number;
  description?: string | null;
  /** Installations built from this repository. Many-to-many, not an owning application. */
  installationIds: number[];
}

export interface AppRepositoryUpsert {
  repositoryUrl: string;
  repositoryType: number;
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
  /** Single tag: every other facet is one Id, and a list would need an AND/OR decision. */
  tagId?: number | null;
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

/** Names the repositoryType numbers coming from the server enum. */
export const repositoryTypeNames: Record<number, string> = {
  0: 'Unknown',
  1: 'Git',
  2: 'SVN',
  3: 'Bitbucket',
  4: 'Mercurial',
  5: 'TFS',
};
