import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { LookupItem, LookupKind, LookupMetadata } from '../api/types';

/**
 * Every Id-backed dropdown in the app, keyed by kind.
 *
 * A record rather than a fixed interface: the kinds come from `GET /api/lookups`, so a lookup
 * added on the server is loaded here without this file changing. Reading a kind that has not
 * arrived yet gives an empty list, which is also the state during the first load.
 */
export type Lookups = Partial<Record<LookupKind, LookupItem[]>>;

const none: LookupItem[] = [];

/** Reader that never returns undefined, so callers can map straight over the result. */
export const itemsOf = (lookups: Lookups, kind: LookupKind): LookupItem[] =>
  lookups[kind] ?? none;

/**
 * Name limit for a kind, from the server's reading of the EF model. The fallback only applies
 * before metadata has arrived, and the server check is the real one either way.
 */
export const maxNameLengthOf = (metadata: LookupMetadata[], kind: LookupKind): number =>
  metadata.find((meta) => meta.kind === kind)?.maxNameLength ?? 512;

/**
 * Loads every lookup table at once. The whole UI is Id-backed, so no form can render before these
 * arrive.
 *
 * The error is surfaced deliberately: silently empty dropdowns read as "there are no machines yet"
 * rather than "the request failed", which was a real bug fixed on 2026-07-29.
 */
export function useLookups() {
  const [lookups, setLookups] = useState<Lookups>({});
  const [metadata, setMetadata] = useState<LookupMetadata[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const kinds = await api.getLookupMetadata();

      const loaded = await Promise.all(
        kinds.map(async (meta) => [meta.kind, await api.getLookup(meta.kind)] as const),
      );

      setMetadata(kinds);
      setLookups(Object.fromEntries(loaded) as Lookups);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load lookups.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  // Metadata comes back too: it was fetched to know what to load, and every caller that renders a
  // lookup field needs its name limit anyway.
  return { lookups, metadata, isLoading, error, reload };
}
