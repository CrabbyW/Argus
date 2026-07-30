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
  /** Only meaningful for appstages. Must be round-tripped on edit or ordering is lost. */
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

/** Matches the `LookupKind` enum on the server (route is case-insensitive). */
export type LookupKind =
  | 'machines'
  | 'applications'
  | 'appstages'
  | 'processorarchitectures'
  | 'dnsendpoints';

export interface InstallationListItem {
  id: number;
  machineName: string;
  appName: string;
  appStageName: string;
  processorArchitecture: string;
  dnsName?: string | null;
  rootPath: string;
  physicalPath?: string | null;
  tags?: string | null;
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
}

export interface AppRepository {
  id: number;
  applicationId: number;
  repositoryUrl: string;
  repositoryType: number;
  description?: string | null;
}

export interface AppRepositoryUpsert {
  applicationId: number;
  repositoryUrl: string;
  repositoryType: number;
  description?: string | null;
}

export interface InstallationDetail {
  id: number;
  machineId: number;
  machineName: string;
  applicationId: number;
  appName: string;
  appStageId: number;
  appStageName: string;
  processorArchitectureId: number;
  processorArchitecture: string;
  dnsEndpointId?: number | null;
  dnsName?: string | null;
  rootPath: string;
  physicalPath?: string | null;
  tags?: string | null;
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
  createdUtc: string;
  modifiedUtc?: string | null;
  appRepositories: AppRepository[];
}

export interface InstallationUpsert {
  machineId: number;
  applicationId: number;
  appStageId: number;
  processorArchitectureId: number;
  dnsEndpointId?: number | null;
  rootPath: string;
  physicalPath?: string | null;
  tags?: string | null;
  isActive: boolean;
  validFromDate: string;
  validToDate?: string | null;
}

export interface InstallationFilter {
  pageNumber?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  searchTerm?: string;
  machineId?: number | null;
  applicationId?: number | null;
  appStageId?: number | null;
  processorArchitectureId?: number | null;
  dnsEndpointId?: number | null;
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
