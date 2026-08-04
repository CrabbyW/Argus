import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Switch,
  Text,
  Title3,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import { ArrowClockwiseRegular, DismissRegular, SearchRegular } from '@fluentui/react-icons';
import { api } from '../api/client';
import type { LogContent, LogFile } from '../api/types';

/** How many lines of the tail to ask for. The server clamps anything larger at 5000. */
const LINE_LIMITS = [200, 500, 1000, 5000];

const DEFAULT_LIMIT = 500;

/** Slow enough not to hammer the API, quick enough to watch a request land while testing. */
const AUTO_REFRESH_MS = 10000;

const useStyles = makeStyles({
  root: { display: 'flex', flexDirection: 'column', rowGap: '16px' },
  pageHeader: { display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' },
  spacer: { flexGrow: 1 },
  muted: { color: tokens.colorNeutralForeground3 },
  controls: { display: 'flex', alignItems: 'flex-end', gap: '12px', flexWrap: 'wrap' },
  file: { minWidth: '260px' },
  search: { minWidth: '260px', flexGrow: 1, maxWidth: '520px' },

  // The log itself. Monospaced and not wrapped: every line in the action log is one record with
  // its fields in fixed order, and reflowing them would break the column the eye follows down.
  // The pane scrolls in both directions rather than the page doing it, so the controls above
  // stay put while a long file is read.
  pane: {
    backgroundColor: tokens.colorNeutralBackground1,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: tokens.borderRadiusMedium,
    padding: '8px 4px',
    height: 'calc(100vh - 300px)',
    minHeight: '320px',
    overflow: 'auto',
  },
  line: {
    display: 'block',
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase300,
    whiteSpace: 'pre',
    padding: '0 8px',
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover },
  },
  errorLine: { color: tokens.colorPaletteRedForeground1 },
  warnLine: { color: tokens.colorPaletteDarkOrangeForeground1 },
  empty: { padding: '16px', color: tokens.colorNeutralForeground3 },
});

function formatUtc(value: string | null | undefined): string {
  if (!value) {
    return '';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? '' : parsed.toLocaleString();
}

function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  const kb = bytes / 1024;
  return kb < 1024 ? `${Math.round(kb)} KB` : `${(kb / 1024).toFixed(1)} MB`;
}

/** The action log ends every line with its outcome: `[404 NotFound]`. */
const ACTION_STATUS = /\[(\d{3}) [A-Za-z]+\]\s*$/;

/** The diagnostic layout writes the level as a word between the thread and the logger name. */
const DIAGNOSTIC_LEVEL = /\b(FATAL|ERROR|WARN)\b/;

/**
 * Which lines are worth catching the eye.
 *
 * Both tests are anchored on purpose. Searching the whole line for "5xx" or for the word
 * "NotFound" seemed simpler and was wrong: it painted successful requests red because the
 * timestamp ended in `.455`, or because the body happened to carry `"maxLines":500`. A status is
 * only a status where the format puts one.
 */
function severityOf(line: string): 'error' | 'warn' | null {
  const status = ACTION_STATUS.exec(line);

  if (status) {
    const code = Number(status[1]);

    if (code >= 500) return 'error';
    if (code >= 400) return 'warn';

    return null;
  }

  const level = DIAGNOSTIC_LEVEL.exec(line);

  if (!level) {
    return null;
  }

  return level[1] === 'WARN' ? 'warn' : 'error';
}

/**
 * The log files, read from the app.
 *
 * Argus already writes two of them — the action log, one line per API request, and the
 * diagnostic log — but until now reading either meant a session on the machine hosting the API
 * and a text editor. That is the wrong tool for the question they answer ("what exactly did that
 * user send, and what did the server reply?"), so the files are served here instead.
 *
 * Read-only on purpose, all the way down to the controller: an audit trail whose own screen can
 * delete it is not an audit trail. Files expire only by `AuditLog:RetentionDays`.
 */
export function LogsPage() {
  const styles = useStyles();
  const [searchParams, setSearchParams] = useSearchParams();

  // In the URL, so a colleague can be sent the exact view — this file, this filter.
  const selectedName = searchParams.get('file') ?? '';
  const searchTerm = searchParams.get('q') ?? '';
  const limit = Number(searchParams.get('lines')) || DEFAULT_LIMIT;

  const [files, setFiles] = useState<LogFile[]>([]);
  const [content, setContent] = useState<LogContent | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [autoRefresh, setAutoRefresh] = useState(false);

  // The search box is typed into freely and only committed on Enter or the button; putting every
  // keystroke in the URL would mean a request per character against a file this size.
  const [searchDraft, setSearchDraft] = useState(searchTerm);

  const paneRef = useRef<HTMLDivElement>(null);

  function updateParams(changes: Record<string, string | null>) {
    const params = new URLSearchParams(searchParams);

    Object.entries(changes).forEach(([key, value]) => {
      if (value === null || value === '') {
        params.delete(key);
      } else {
        params.set(key, value);
      }
    });

    setSearchParams(params, { replace: true });
  }

  const loadFiles = useCallback(async () => {
    try {
      const list = await api.getLogFiles();
      setFiles(list);
      return list;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to list the log files.');
      return [];
    }
  }, []);

  const loadContent = useCallback(
    async (name: string, lines: number, term: string, showSpinner: boolean) => {
      if (!name) {
        setContent(null);
        setIsLoading(false);
        return;
      }

      if (showSpinner) {
        setIsLoading(true);
      }

      try {
        setContent(await api.getLogContent(name, lines, term || undefined));
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to read the log file.');
      } finally {
        setIsLoading(false);
      }
    },
    [],
  );

  // First load: list the files, and if the URL named none, open the newest — the screen is
  // opened to look at what just happened far more often than at a particular old file.
  useEffect(() => {
    let cancelled = false;

    void (async () => {
      const list = await loadFiles();

      if (cancelled) {
        return;
      }

      if (!selectedName && list.length > 0) {
        updateParams({ file: list[0].name });
      } else if (list.length === 0) {
        setIsLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
    // Deliberately once: this only picks the initial file.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    void loadContent(selectedName, limit, searchTerm, true);
  }, [selectedName, limit, searchTerm, loadContent]);

  useEffect(() => {
    setSearchDraft(searchTerm);
  }, [searchTerm]);

  // Newest line last, so the useful end of the file is the one on screen after a load.
  useEffect(() => {
    if (paneRef.current) {
      paneRef.current.scrollTop = paneRef.current.scrollHeight;
    }
  }, [content]);

  useEffect(() => {
    if (!autoRefresh || !selectedName) {
      return;
    }

    // No spinner on these: a pane that blanks itself every ten seconds is unreadable.
    const timer = window.setInterval(
      () => void loadContent(selectedName, limit, searchTerm, false),
      AUTO_REFRESH_MS,
    );

    return () => window.clearInterval(timer);
  }, [autoRefresh, selectedName, limit, searchTerm, loadContent]);

  const selectedFile = files.find((file) => file.name === selectedName);

  return (
    <div className={styles.root}>
      <div className={styles.pageHeader}>
        <Title3>Logs</Title3>

        {selectedFile && (
          <Badge appearance="tint" color={selectedFile.kind === 'action' ? 'brand' : 'informative'}>
            {selectedFile.kind === 'action' ? 'Action log' : 'Diagnostic log'}
          </Badge>
        )}

        {selectedFile && (
          <Text className={styles.muted}>
            {formatSize(selectedFile.sizeBytes)} · written {formatUtc(selectedFile.lastWriteUtc)}
          </Text>
        )}

        <div className={styles.spacer} />

        <Switch
          label="Auto-refresh"
          checked={autoRefresh}
          onChange={(_, data) => setAutoRefresh(data.checked)}
        />

        <Button
          icon={<ArrowClockwiseRegular />}
          onClick={() => {
            void loadFiles();
            void loadContent(selectedName, limit, searchTerm, true);
          }}
        >
          Refresh
        </Button>
      </div>

      <div className={styles.controls}>
        <Field label="File" className={styles.file}>
          <Dropdown
            aria-label="File"
            value={selectedName}
            selectedOptions={selectedName ? [selectedName] : []}
            onOptionSelect={(_, data) => updateParams({ file: data.optionValue ?? null })}
            placeholder={files.length === 0 ? 'No log files yet' : 'Pick a file'}
            disabled={files.length === 0}
          >
            {files.map((file) => (
              <Option key={file.name} value={file.name} text={file.name}>
                {`${file.name}  (${formatSize(file.sizeBytes)})`}
              </Option>
            ))}
          </Dropdown>
        </Field>

        <Field label="Contains" className={styles.search}>
          <Input
            value={searchDraft}
            placeholder="Filter lines, e.g. Installations_CreateInstallation"
            contentBefore={<SearchRegular />}
            contentAfter={
              searchDraft ? (
                <Button
                  appearance="transparent"
                  size="small"
                  icon={<DismissRegular />}
                  onClick={() => {
                    setSearchDraft('');
                    updateParams({ q: null });
                  }}
                />
              ) : undefined
            }
            onChange={(_, data) => setSearchDraft(data.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                updateParams({ q: searchDraft });
              }
            }}
            onBlur={() => updateParams({ q: searchDraft })}
          />
        </Field>

        <Field label="Lines">
          <Dropdown
            aria-label="Lines"
            value={String(limit)}
            selectedOptions={[String(limit)]}
            onOptionSelect={(_, data) => updateParams({ lines: data.optionValue ?? null })}
          >
            {LINE_LIMITS.map((value) => (
              <Option key={value} value={String(value)} text={String(value)}>
                {`Last ${value}`}
              </Option>
            ))}
          </Dropdown>
        </Field>
      </div>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {content?.isTruncated && (
        <MessageBar intent="info">
          <MessageBarBody>
            Showing the last {content.lines.length} of {content.totalLines} matching lines. Raise
            the line count or narrow the filter to see more.
          </MessageBarBody>
        </MessageBar>
      )}

      {isLoading ? (
        <Spinner label="Reading the log..." />
      ) : (
        <div className={styles.pane} ref={paneRef}>
          {!content || content.lines.length === 0 ? (
            <div className={styles.empty}>
              {files.length === 0
                ? 'No log files on the server yet.'
                : searchTerm
                  ? `Nothing in ${selectedName} matches "${searchTerm}".`
                  : `${selectedName} is empty.`}
            </div>
          ) : (
            content.lines.map((line, index) => {
              const severity = severityOf(line);

              return (
                <span
                  // Lines repeat verbatim in a log, so the position is the only stable key —
                  // and the list is replaced wholesale on every load, never reordered.
                  key={index}
                  // mergeClasses, not a template string: Griffel's atomic classes have to be
                  // merged by it or the last one silently wins.
                  className={mergeClasses(
                    styles.line,
                    severity === 'error' ? styles.errorLine : undefined,
                    severity === 'warn' ? styles.warnLine : undefined,
                  )}
                >
                  {line}
                </span>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}
