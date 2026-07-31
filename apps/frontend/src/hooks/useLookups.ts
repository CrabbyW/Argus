import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { LookupItem } from '../api/types';

/**
 * Every Id-backed dropdown in the app. Nine entries: the eight editable lookups plus
 * repositories, which the installation form offers as a multiselect but writes through its
 * own endpoint.
 */
export interface Lookups {
  machines: LookupItem[];
  appNames: LookupItem[];
  appStageNames: LookupItem[];
  processorArchitectures: LookupItem[];
  dnsEndpoints: LookupItem[];
  rootPaths: LookupItem[];
  physicalPaths: LookupItem[];
  tags: LookupItem[];
  repositories: LookupItem[];
}

const empty: Lookups = {
  machines: [],
  appNames: [],
  appStageNames: [],
  processorArchitectures: [],
  dnsEndpoints: [],
  rootPaths: [],
  physicalPaths: [],
  tags: [],
  repositories: [],
};

/**
 * Loads all nine lookup tables at once. The whole UI is Id-backed, so no form can render
 * before these arrive.
 *
 * The error is surfaced deliberately: silently empty dropdowns read as "there are no machines
 * yet" rather than "the request failed", which was a real bug fixed on 2026-07-29.
 */
export function useLookups() {
  const [lookups, setLookups] = useState<Lookups>(empty);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [
        machines,
        appNames,
        appStageNames,
        processorArchitectures,
        dnsEndpoints,
        rootPaths,
        physicalPaths,
        tags,
        repositories,
      ] = await Promise.all([
        api.getLookup('machines'),
        api.getLookup('appnames'),
        api.getLookup('appstagenames'),
        api.getLookup('processorarchitectures'),
        api.getLookup('dnsendpoints'),
        api.getLookup('rootpaths'),
        api.getLookup('physicalpaths'),
        api.getLookup('tags'),
        api.getLookup('apprepositories'),
      ]);

      setLookups({
        machines,
        appNames,
        appStageNames,
        processorArchitectures,
        dnsEndpoints,
        rootPaths,
        physicalPaths,
        tags,
        repositories,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load lookups.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { lookups, isLoading, error, reload };
}
