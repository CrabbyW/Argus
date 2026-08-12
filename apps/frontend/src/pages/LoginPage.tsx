import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ModernLogin } from '../components/ui/modern-login';

/**
 * Sign-in. The screen itself is `components/ui/modern-login`, which knows nothing about Argus's
 * auth — this page holds the call and the two pieces of state the form has to be told about.
 */
export function LoginPage() {
  const { login } = useAuth();

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(username: string, password: string) {
    setError(null);
    setIsSubmitting(true);

    try {
      await login(username, password);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  return <ModernLogin onSubmit={handleSubmit} error={error} isSubmitting={isSubmitting} />;
}
