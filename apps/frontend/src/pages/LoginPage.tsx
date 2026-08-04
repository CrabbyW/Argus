import { useState } from 'react';
import type { FormEvent } from 'react';
import {
  Button,
  Card,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Title2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useAuth } from '../auth/AuthContext';
import { ArgusMark } from '../components/ArgusMark';

const useStyles = makeStyles({
  page: {
    minHeight: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: tokens.colorNeutralBackground2,
    padding: '16px',
  },
  card: {
    width: '100%',
    maxWidth: '380px',
    padding: '24px',
    display: 'flex',
    flexDirection: 'column',
    rowGap: '16px',
  },
  identity: { display: 'flex', alignItems: 'center', gap: '14px' },
  mark: { color: tokens.colorBrandForeground1, flexShrink: 0 },
  subtitle: {
    color: tokens.colorNeutralForeground3,
    margin: 0,
  },
});

export function LoginPage() {
  const styles = useStyles();
  const { login } = useAuth();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
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

  return (
    <div className={styles.page}>
      <Card className={styles.card}>
        {/* The one screen with room for the mark at a size worth showing it — 40px, well
            over the 24px floor `brand/README.md` sets for the standalone mark. */}
        <div className={styles.identity}>
          <ArgusMark size={40} className={styles.mark} />
          <div>
            <Title2>Argus</Title2>
            <p className={styles.subtitle}>Installation inventory</p>
          </div>
        </div>

        {error && (
          <MessageBar intent="error">
            <MessageBarBody>{error}</MessageBarBody>
          </MessageBar>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', rowGap: '16px' }}>
          <Field label="Username" required>
            <Input
              value={username}
              onChange={(_, data) => setUsername(data.value)}
              autoComplete="username"
              autoFocus
            />
          </Field>

          <Field label="Password" required>
            <Input
              type="password"
              value={password}
              onChange={(_, data) => setPassword(data.value)}
              autoComplete="current-password"
            />
          </Field>

          <Button appearance="primary" type="submit" disabled={isSubmitting || !username || !password}>
            {isSubmitting ? <Spinner size="tiny" label="Signing in..." /> : 'Sign in'}
          </Button>
        </form>
      </Card>
    </div>
  );
}
