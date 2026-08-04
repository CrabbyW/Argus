import { Link as RouterLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  Button,
  Menu,
  MenuItemRadio,
  MenuList,
  MenuPopover,
  MenuTrigger,
  Spinner,
  Tab,
  TabList,
  Text,
  Title3,
  Toaster,
  Tooltip,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import {
  DesktopRegular,
  SignOutRegular,
  WeatherMoonRegular,
  WeatherSunnyRegular,
} from '@fluentui/react-icons';
import { useAuth } from './auth/AuthContext';
import { useTheme } from './theme/ThemeContext';
import { ArgusMark } from './components/ArgusMark';
import { TOASTER_ID } from './hooks/useAppToast';
import { LoginPage } from './pages/LoginPage';
import { InstallationsPage } from './pages/InstallationsPage';
import { LogsPage } from './pages/LogsPage';
import { LookupsPage } from './pages/LookupsPage';
import { RepositoriesPage } from './pages/RepositoriesPage';
import { UsersPage } from './pages/UsersPage';

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
  // Mark plus wordmark, one unit. `brand/README.md` sets the clear space at one tile —
  // 12 of the mark's 64 units, so 24px of mark buys 4.5px around it, and the header's own
  // gap covers the rest.
  brand: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '9px',
    paddingRight: '5px',
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase500,
    color: tokens.colorNeutralForeground1,
    textDecoration: 'none',
  },
  // The mark carries the brand colour; the wordmark stays in text colour, as in the logo
  // files, where only the tiles are blue.
  mark: { color: tokens.colorBrandForeground1, flexShrink: 0 },
  spacer: { flexGrow: 1 },
  // Wide on purpose. The installations grid is twelve columns and needs about 1530px before it
  // has to scroll sideways; a 1500px cap meant a full-screen window still showed a horizontal
  // scrollbar under the table, which is the one place the extra pixels were there for. The cap
  // that remains only stops text pages from running to a 4K width.
  main: { maxWidth: '2200px', margin: '0 auto', padding: '24px' },
  center: { display: 'flex', justifyContent: 'center', paddingTop: '80px' },
  muted: { color: tokens.colorNeutralForeground3 },
  notFound: { display: 'flex', flexDirection: 'column', rowGap: '12px', alignItems: 'flex-start' },
});

/** The tab whose section owns the current URL. */
function activeTab(pathname: string): string {
  if (pathname.startsWith('/lookups')) return 'lookups';
  if (pathname.startsWith('/repositories')) return 'repositories';
  if (pathname.startsWith('/users')) return 'users';
  if (pathname.startsWith('/logs')) return 'logs';
  return 'installations';
}

/**
 * Light/dark with "System" kept as an option rather than only a two-way flip: a machine that
 * switches itself at dusk should be able to go on doing that after someone has once looked at
 * the menu. The button shows the theme currently on screen, not the mode that was chosen.
 */
function ThemeMenu() {
  const { mode, isDark, setMode } = useTheme();

  return (
    <Menu
      checkedValues={{ theme: [mode] }}
      onCheckedValueChange={(_, data) => setMode(data.checkedItems[0])}
    >
      <MenuTrigger disableButtonEnhancement>
        <Tooltip content="Theme" relationship="label">
          <Button
            appearance="subtle"
            icon={isDark ? <WeatherMoonRegular /> : <WeatherSunnyRegular />}
          />
        </Tooltip>
      </MenuTrigger>

      <MenuPopover>
        <MenuList>
          <MenuItemRadio name="theme" value="light" icon={<WeatherSunnyRegular />}>
            Light
          </MenuItemRadio>
          <MenuItemRadio name="theme" value="dark" icon={<WeatherMoonRegular />}>
            Dark
          </MenuItemRadio>
          <MenuItemRadio name="theme" value="system" icon={<DesktopRegular />}>
            System
          </MenuItemRadio>
        </MenuList>
      </MenuPopover>
    </Menu>
  );
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
          <ArgusMark size={24} className={styles.mark} />
          Argus
        </RouterLink>

        <TabList
          selectedValue={activeTab(location.pathname)}
          onTabSelect={(_, data) => navigate(`/${data.value}`)}
        >
          <Tab value="installations">Installations</Tab>
          <Tab value="lookups">Lookups</Tab>
          <Tab value="repositories">Repositories</Tab>
          <Tab value="users">Users</Tab>
          <Tab value="logs">Logs</Tab>
        </TabList>

        <div className={styles.spacer} />

        <Text className={styles.muted}>{user.displayName}</Text>
        <ThemeMenu />
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
          <Route path="/users" element={<UsersPage />} />
          <Route path="/logs" element={<LogsPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>

      <Toaster toasterId={TOASTER_ID} position="top-end" />
    </div>
  );
}
