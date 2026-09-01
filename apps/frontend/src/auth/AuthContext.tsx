import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { api, setUnauthorizedHandler, tokenStorage } from '../api/client';
import type { CurrentUser } from '../api/types';

interface AuthContextValue {
  user: CurrentUser | null;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  /** Signs in with the browser's Windows account. Rejects like `login` does. */
  loginWithWindows: () => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Any rejected token ends the session, wherever in the app it was noticed.
  useEffect(() => {
    setUnauthorizedHandler(() => setUser(null));
    return () => setUnauthorizedHandler(null);
  }, []);

  // On boot, a stored token may still be valid — ask the server rather than trusting it.
  useEffect(() => {
    if (!tokenStorage.get()) {
      setIsLoading(false);
      return;
    }

    api
      .getCurrentUser()
      .then(setUser)
      .catch(() => {
        tokenStorage.clear();
        setUser(null);
      })
      .finally(() => setIsLoading(false));
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    const result = await api.login({ username, password });
    tokenStorage.set(result.token);

    const current = await api.getCurrentUser();
    setUser(current);
  }, []);

  // The Windows handshake happens inside the request; from here the two ways in differ only in
  // which call issues the token.
  const loginWithWindows = useCallback(async () => {
    const result = await api.loginWithWindows();
    tokenStorage.set(result.token);

    const current = await api.getCurrentUser();
    setUser(current);
  }, []);

  const logout = useCallback(() => {
    // Told to the server first, because the call needs the token that the next line drops. It is
    // only there to be logged, so a failing one must not keep anybody signed in.
    void api.logout().catch(() => undefined);

    tokenStorage.clear();
    setUser(null);
  }, []);

  const value = useMemo(
    () => ({ user, isLoading, login, loginWithWindows, logout }),
    [user, isLoading, login, loginWithWindows, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuth must be used inside an AuthProvider.');
  }

  return context;
}
