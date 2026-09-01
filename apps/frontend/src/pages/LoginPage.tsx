import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { api } from '../api/client';
import { ModernLogin } from '../components/ui/modern-login';

/**
 * Sign-in. The screen itself is `components/ui/modern-login`, which knows nothing about Argus's
 * auth — this page holds the calls and the pieces of state the form has to be told about.
 *
 * There are two ways in: the username + password form, and the browser's Windows account. Which
 * of them exists is the server's answer, not a build-time choice, so the Windows button appears
 * only after `GET /api/auth/options` says the server will honour it.
 */
export function LoginPage() {
  const { login, loginWithWindows } = useAuth();
  const navigate = useNavigate();

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isWindowsAuthEnabled, setIsWindowsAuthEnabled] = useState(false);

  useEffect(() => {
    // A server that cannot answer this is one the sign-in itself will fail against; there is
    // nothing useful to say here beyond leaving the button off, and the form still works.
    api
      .getAuthOptions()
      .then((options) => setIsWindowsAuthEnabled(options.windowsAuthEnabled))
      .catch(() => setIsWindowsAuthEnabled(false));
  }, []);

  /**
   * Signing in always lands on the installations grid, whatever address the session ended on. It
   * is the screen the app is opened for, and after a sign-out the previous page is as likely to
   * be one someone else left behind as one worth returning to.
   */
  async function signIn(attempt: () => Promise<void>, fallbackMessage: string) {
    setError(null);
    setIsSubmitting(true);

    try {
      await attempt();
      navigate('/installations', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : fallbackMessage);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <ModernLogin
      onSubmit={(username, password) =>
        signIn(() => login(username, password), 'Login failed.')
      }
      onWindowsSignIn={
        isWindowsAuthEnabled
          ? () =>
              signIn(
                () => loginWithWindows(),
                // The usual cause is a browser that would not negotiate — it is not in the domain,
                // or the site is not one it trusts — and the server never saw an account at all.
                'Windows sign-in failed. Your browser did not provide a Windows account.',
              )
          : undefined
      }
      error={error}
      isSubmitting={isSubmitting}
    />
  );
}
