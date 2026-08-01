import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { LookupMetadata } from '../api/types';

/**
 * The list of lookup kinds and how to render each one, from `GET /api/lookups`.
 *
 * This is what makes the Lookups screen generic: tabs, form fields and name-length limits are all
 * read from here, so adding a lookup on the server is enough to make it appear in the UI. Before
 * this, the same facts lived in a hand-written tab list and a hand-written length table that had
 * to be updated in step with the backend and silently disagreed when they were not.
 */
export function useLookupMetadata() {
  const [metadata, setMetadata] = useState<LookupMetadata[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      setMetadata(await api.getLookupMetadata());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load lookup metadata.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { metadata, isLoading, error, reload };
}
