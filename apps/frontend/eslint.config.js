import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';

// Flat config (ESLint 10). The hook rules are listed by name rather than spread from the
// plugin's own preset, because that preset's shape has changed between plugin majors and a
// renamed export would silently disable them instead of failing.
export default tseslint.config(
  { ignores: ['dist', 'vite.config.ts'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
  {
    // The entry point mounts the app and is not a fast-refresh boundary, so the rule that wants
    // every file to export a component does not apply to it.
    files: ['src/main.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  {
    // A provider and the hook that reads it belong in one file; splitting them to satisfy fast
    // refresh would spread one concern over two.
    files: ['src/auth/AuthContext.tsx', 'src/theme/ThemeContext.tsx'],
    rules: {
      'react-refresh/only-export-components': [
        'warn',
        { allowExportNames: ['useAuth', 'useTheme'] },
      ],
    },
  },
);
