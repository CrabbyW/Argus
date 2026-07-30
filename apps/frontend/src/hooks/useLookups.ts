import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { LookupItem } from '../api/types';

export interface Lookups {
  machines: LookupItem[];
  applications: LookupItem[];
  appStages: LookupItem[];
  processorArchitectures: LookupItem[];
  dnsEndpoints: LookupItem[];
}

const empty: Lookups = {
  machines: [],
  applications: [],
  appStages: [],
  processorArchitectures: [],
  dnsEndpoints: [],
};

/**
 * Loads all five lookup tables at once. Every dropdown in the app is Id-backed,
 * so the whole UI needs these before it can render a form.
 */
export function useLookups() {
  const [lookups, setLookups] = useState<Lookups>(empty);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [machines, applications, appStages, processorArchitectures, dnsEndpoints] =
        await Promise.all([
          api.getLookup('machines'),
          api.getLookup('applications'),
          api.getLookup('appstages'),
          api.getLookup('processorarchitectures'),
          api.getLookup('dnsendpoints'),
        ]);

      setLookups({ machines, applications, appStages, processorArchitectures, dnsEndpoints });
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
