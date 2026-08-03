import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

const DARK_QUERY = '(prefers-color-scheme: dark)';
const THEME_STORAGE_KEY = 'argus.theme';

/**
 * `system` is the starting point and stays live — the app follows the OS until someone picks
 * a side, and only then stops listening. A chosen mode is a preference about the machine, not
 * about the session, so unlike the auth token it belongs in `localStorage` and survives the
 * browser being closed.
 */
type ThemeMode = 'system' | 'light' | 'dark';

interface ThemeContextValue {
  mode: ThemeMode;
  isDark: boolean;
  /**
   * Takes a plain string because its callers are menus, which deal in option values. Knowing
   * which strings are modes is this module's job, so anything unrecognised falls back to
   * `system` rather than being asserted into the type at the call site.
   */
  setMode: (mode: string) => void;
  /** Flips to the opposite of what is on screen, whichever way `system` happens to point. */
  toggle: () => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function toMode(value: string | null): ThemeMode {
  return value === 'light' || value === 'dark' || value === 'system' ? value : 'system';
}

function readStoredMode(): ThemeMode {
  return toMode(localStorage.getItem(THEME_STORAGE_KEY));
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(readStoredMode);
  const [systemIsDark, setSystemIsDark] = useState(() => window.matchMedia(DARK_QUERY).matches);

  // Tracked even while a manual mode is active: switching back to `system` must land on what
  // the OS says now, not on what it said when the app started.
  useEffect(() => {
    const media = window.matchMedia(DARK_QUERY);
    const onChange = (event: MediaQueryListEvent) => setSystemIsDark(event.matches);

    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, []);

  const setMode = useCallback((value: string) => {
    const next = toMode(value);
    setModeState(next);

    if (next === 'system') {
      localStorage.removeItem(THEME_STORAGE_KEY);
    } else {
      localStorage.setItem(THEME_STORAGE_KEY, next);
    }
  }, []);

  const isDark = mode === 'system' ? systemIsDark : mode === 'dark';

  const toggle = useCallback(() => setMode(isDark ? 'light' : 'dark'), [isDark, setMode]);

  const value = useMemo<ThemeContextValue>(
    () => ({ mode, isDark, setMode, toggle }),
    [mode, isDark, setMode, toggle],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error('useTheme must be used inside a ThemeProvider.');
  }

  return context;
}
