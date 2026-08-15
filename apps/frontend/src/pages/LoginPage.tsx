import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { ModernLogin } from '../components/ui/modern-login';

/**
 * Sign-in. The screen itself is `components/ui/modern-login`, which knows nothing about Argus's
 * auth — this page holds the call and the two pieces of state the form has to be told about.
 */
export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(username: string, password: string) {
    setError(null);
    setIsSubmitting(true);

    try {
      await login(username, password);

      // Signing in always lands on the installations grid, whatever address the session ended
      // on. It is the screen the app is opened for, and after a sign-out the previous page is
      // as likely to be one someone else left behind as one worth returning to.
      navigate('/installations', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return <ModernLogin onSubmit={handleSubmit} error={error} isSubmitting={isSubmitting} />;
}
