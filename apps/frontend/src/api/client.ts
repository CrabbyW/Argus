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
  LookupMetadata,
  LookupUpsert,
  LoginRequest,
  LoginResponse,
  User,
  UserUpsert,
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

/**
 * The token lives in `sessionStorage`, not `localStorage`: closing the browser must end the
 * session no matter what, so reopening Argus always starts at the sign-in screen. Within a
 * tab it survives reloads and navigation, and the server's own 8-hour token lifetime still
 * ends a session that stays open all day.
 *
 * A token left behind by an earlier `localStorage` build is dropped on load — otherwise it
 * would sit there unused and outlive every session it was supposed to be scoped to.
 */
localStorage.removeItem(TOKEN_STORAGE_KEY);

export const tokenStorage = {
  get: () => sessionStorage.getItem(TOKEN_STORAGE_KEY),
  set: (token: string) => sessionStorage.setItem(TOKEN_STORAGE_KEY, token),
  clear: () => sessionStorage.removeItem(TOKEN_STORAGE_KEY),
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

/**
 * A read sent as a POST: criteria in the body, never in the query string.
 *
 * `requestUrl` goes with them. Once the criteria move into the body every search shares one
 * address — `POST /api/installations/search` — and the server's action log would record that
 * same line whatever was asked for. The full address of the equivalent request is therefore
 * carried in the payload, so the log keeps saying which search was run.
 */
function read<T>(
  path: string,
  body: Record<string, unknown> = {},
  /**
   * What this read is, as an address: the resource plus its criteria, which is what the log is
   * for. Defaults to the posted path — pass it explicitly wherever that path is a `/search`
   * endpoint, since `/installations/search` names the mechanism and not the question.
   */
  describedAs = path,
): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    body: JSON.stringify({
      ...body,
      requestUrl: `${window.location.origin}/api${describedAs}`,
    }),
  });
}

export const api = {
  login: (payload: LoginRequest) =>
    request<LoginResponse>('/auth/login', { method: 'POST', body: JSON.stringify(payload) }),

  getCurrentUser: () => read<CurrentUser>('/auth/me'),

  getInstallations: (filter: InstallationFilter) =>
    read<DataViewOutput<InstallationListItem>>(
      '/installations/search',
      { ...filter },
      `/installations${toQueryString(filter)}`,
    ),

  getInstallation: (id: number) =>
    read<InstallationDetail>(`/installations/${id}/read`, {}, `/installations/${id}`),

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

  /** Every lookup kind and how to render it. The Lookups screen starts here. */
  getLookupMetadata: () => read<LookupMetadata[]>('/lookups/search', {}, '/lookups'),

  getLookup: (kind: LookupKind) =>
    read<LookupItem[]>(`/lookups/${kind}/search`, {}, `/lookups/${kind}`),

  createLookupItem: (kind: LookupKind, payload: LookupUpsert) =>
    request<LookupItem>(`/lookups/${kind}`, { method: 'POST', body: JSON.stringify(payload) }),

  updateLookupItem: (kind: LookupKind, id: number, payload: LookupUpsert) =>
    request<LookupItem>(`/lookups/${kind}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteLookupItem: (kind: LookupKind, id: number) =>
    request<boolean>(`/lookups/${kind}/${id}`, { method: 'DELETE' }),

  /**
   * Repositories are linked to installations many-to-many — one repository row, several links.
   * Both filters are optional and independent: `installationId` answers "what is this
   * installation built from", `appNameId` answers "what does this application use anywhere".
   */
  getRepositories: (filter: { installationId?: number | null; appNameId?: number | null } = {}) => {
    const params = new URLSearchParams();
    if (filter.installationId) {
      params.set('installationId', String(filter.installationId));
    }
    if (filter.appNameId) {
      params.set('appNameId', String(filter.appNameId));
    }
    const query = params.toString();

    return read<AppRepository[]>(
      '/apprepositories/search',
      { installationId: filter.installationId ?? null, appNameId: filter.appNameId ?? null },
      `/apprepositories${query ? `?${query}` : ''}`,
    );
  },

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

  /**
   * Accounts. `includeDisabled` exists because a soft-deleted user is invisible to every other
   * query — without it there would be no way to restore one short of editing the database.
   */
  getUsers: (includeDisabled = false) =>
    read<User[]>(
      '/users/search',
      { includeDisabled },
      `/users${includeDisabled ? '?includeDisabled=true' : ''}`,
    ),

  createUser: (payload: UserUpsert) =>
    request<User>('/users', { method: 'POST', body: JSON.stringify(payload) }),

  updateUser: (id: number, payload: UserUpsert) =>
    request<User>(`/users/${id}`, { method: 'PUT', body: JSON.stringify(payload) }),

  /** Its own call, so an ordinary edit can never carry a password by accident. */
  setUserPassword: (id: number, password: string) =>
    request<boolean>(`/users/${id}/password`, {
      method: 'POST',
      body: JSON.stringify({ password }),
    }),

  disableUser: (id: number) => request<boolean>(`/users/${id}`, { method: 'DELETE' }),

  restoreUser: (id: number) => request<boolean>(`/users/${id}/restore`, { method: 'POST' }),
};
