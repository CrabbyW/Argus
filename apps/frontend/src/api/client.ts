import type {
  ApiResponse,
  AppRepository,
  AppRepositoryUpsert,
  CurrentUser,
  DataViewOutput,
  ErrorResponse,
  InstallationDetail,
  InstallationFilter,
  InstallationListItem,
  InstallationUpsert,
  LookupItem,
  LookupKind,
  LookupUpsert,
  LoginRequest,
  LoginResponse,
} from './types';

const TOKEN_STORAGE_KEY = 'argus.token';

/** Thrown for any non-2xx response, carrying the server's error code and message. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly errorCode: string,
    message: string,
    public readonly traceId?: string | null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export const tokenStorage = {
  get: () => localStorage.getItem(TOKEN_STORAGE_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_STORAGE_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_STORAGE_KEY),
};

let unauthorizedHandler: (() => void) | null = null;

/**
 * Registered by AuthContext. Dropping the token is not enough on its own — without this the
 * app keeps rendering the signed-in shell and the user just collects error banners.
 * Also fires for a failed sign-in, where clearing an already-empty session is a no-op.
 */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  unauthorizedHandler = handler;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = tokenStorage.get();

  const headers = new Headers(init.headers);
  headers.set('Content-Type', 'application/json');
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(`/api${path}`, { ...init, headers });

  if (response.status === 204) {
    return undefined as T;
  }

  const body = await response.json().catch(() => null);

  if (!response.ok) {
    // An expired or rejected token means the session is over — drop it so the
    // app falls back to the login screen instead of looping on 401s.
    if (response.status === 401) {
      tokenStorage.clear();
      unauthorizedHandler?.();
    }

    const error = body as ErrorResponse | null;
    throw new ApiError(
      response.status,
      error?.errorCode ?? 'UNKNOWN_ERROR',
      error?.message ?? `Request failed with status ${response.status}.`,
      error?.traceId,
    );
  }

  return (body as ApiResponse<T>).data as T;
}

function toQueryString(filter: InstallationFilter): string {
  const params = new URLSearchParams();

  Object.entries(filter).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value));
    }
  });

  const query = params.toString();
  return query ? `?${query}` : '';
}

export const api = {
  login: (payload: LoginRequest) =>
    request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify(payload) }),

  getCurrentUser: () => request<CurrentUser>('/auth/me'),

  getInstallations: (filter: InstallationFilter) =>
    request<DataViewOutput<InstallationListItem>>(`/installations${toQueryString(filter)}`),

  getInstallation: (id: number) => request<InstallationDetail>(`/installations/${id}`),

  createInstallation: (payload: InstallationUpsert) =>
    request<InstallationDetail>('/installations', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateInstallation: (id: number, payload: InstallationUpsert) =>
    request<InstallationDetail>(`/installations/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteInstallation: (id: number) =>
    request<boolean>(`/installations/${id}`, { method: 'DELETE' }),

  getLookup: (kind: LookupKind) => request<LookupItem[]>(`/lookups/${kind}`),

  createLookupItem: (kind: LookupKind, payload: LookupUpsert) =>
    request<LookupItem>(`/lookups/${kind}`, { method: 'POST', body: JSON.stringify(payload) }),

  updateLookupItem: (kind: LookupKind, id: number, payload: LookupUpsert) =>
    request<LookupItem>(`/lookups/${kind}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteLookupItem: (kind: LookupKind, id: number) =>
    request<boolean>(`/lookups/${kind}/${id}`, { method: 'DELETE' }),

  // Repositories hang off an Application, not off a single installation.
  getRepositories: (applicationId?: number | null) =>
    request<AppRepository[]>(
      `/apprepositories${applicationId ? `?applicationId=${applicationId}` : ''}`,
    ),

  createRepository: (payload: AppRepositoryUpsert) =>
    request<AppRepository>('/apprepositories', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateRepository: (id: number, payload: AppRepositoryUpsert) =>
    request<AppRepository>(`/apprepositories/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteRepository: (id: number) =>
    request<boolean>(`/apprepositories/${id}`, { method: 'DELETE' }),
};
