import { Link as RouterLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  Button,
  Spinner,
  Tab,
  TabList,
  Text,
  Title3,
  Toaster,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { SignOutRegular } from '@fluentui/react-icons';
import { useAuth } from './auth/AuthContext';
import { TOASTER_ID } from './hooks/useAppToast';
import { LoginPage } from './pages/LoginPage';
import { InstallationsPage } from './pages/InstallationsPage';
import { LookupsPage } from './pages/LookupsPage';
import { RepositoriesPage } from './pages/RepositoriesPage';

const useStyles = makeStyles({
  shell: { minHeight: '100vh', backgroundColor: tokens.colorNeutralBackground2 },
  header: {
    display: 'flex',
    alignItems: 'center',
    columnGap: '16px',
    rowGap: '4px',
    flexWrap: 'wrap',
    padding: '10px 24px',
    backgroundColor: tokens.colorNeutralBackground1,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    position: 'sticky',
    top: 0,
    zIndex: 10,
  },
  brand: {
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase500,
    color: tokens.colorNeutralForeground1,
    textDecoration: 'none',
  },
  spacer: { flexGrow: 1 },
  main: { maxWidth: '1500px', margin: '0 auto', padding: '24px' },
  center: { display: 'flex', justifyContent: 'center', paddingTop: '80px' },
  muted: { color: tokens.colorNeutralForeground3 },
  notFound: { display: 'flex', flexDirection: 'column', rowGap: '12px', alignItems: 'flex-start' },
});

/** The tab whose section owns the current URL. */
function activeTab(pathname: string): string {
  if (pathname.startsWith('/lookups')) return 'lookups';
  if (pathname.startsWith('/repositories')) return 'repositories';
  return 'installations';
}

function NotFoundPage() {
  const styles = useStyles();

  return (
    <div className={styles.notFound}>
      <Title3>That page does not exist.</Title3>
      <Text className={styles.muted}>The address may be mistyped or the link out of date.</Text>
      <Button appearance="primary" as="a" href="/installations">
        Go to installations
      </Button>
    </div>
  );
}

export function App() {
  const styles = useStyles();
  const { user, isLoading, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className={styles.center}>
        <Spinner label="Starting Argus..." />
      </div>
    );
  }

  // Signed out: every address shows the login screen, and the address itself is kept, so
  // signing in lands on whatever page was originally asked for.
  if (!user) {
    return (
      <>
        <LoginPage />
        <Toaster toasterId={TOASTER_ID} position="top-end" />
      </>
    );
  }

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <RouterLink to="/installations" className={styles.brand}>
          Argus
        </RouterLink>

        <TabList
          selectedValue={activeTab(location.pathname)}
          onTabSelect={(_, data) => navigate(`/${data.value}`)}
        >
          <Tab value="installations">Installations</Tab>
          <Tab value="lookups">Lookups</Tab>
          <Tab value="repositories">Repositories</Tab>
        </TabList>

        <div className={styles.spacer} />

        <Text className={styles.muted}>{user.displayName}</Text>
        <Button appearance="subtle" icon={<SignOutRegular />} onClick={logout}>
          Sign out
        </Button>
      </header>

      <main className={styles.main}>
        <Routes>
          {/*
            The query string is carried across this redirect on purpose. The installations
            filters live entirely in it, so dropping it here would silently turn a shared
            link — "/?machine=2", or any filtered view a colleague was sent — into an
            unfiltered grid, with no error to show for it. Same reason on /login: signing in
            from a filtered address has to land on that filter, not on everything.
          */}
          <Route path="/" element={<Navigate to={{ pathname: '/installations', search: location.search }} replace />} />
          <Route path="/login" element={<Navigate to={{ pathname: '/installations', search: location.search }} replace />} />
          <Route path="/installations" element={<InstallationsPage />} />
          {/* :id is an installation id, "new", or "<id>/view" for the read-only detail. */}
          <Route path="/installations/:id" element={<InstallationsPage />} />
          <Route path="/installations/:id/view" element={<InstallationsPage />} />
          <Route path="/lookups" element={<LookupsPage />} />
          <Route path="/lookups/:kind" element={<LookupsPage />} />
          <Route path="/repositories" element={<RepositoriesPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>

      <Toaster toasterId={TOASTER_ID} position="top-end" />
    </div>
  );
}
