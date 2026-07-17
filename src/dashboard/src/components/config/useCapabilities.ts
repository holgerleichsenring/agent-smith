"use client";

import { useEffect, useState } from "react";
import { fetchCapabilities, type ConfigCapabilities } from "@/lib/configApi";

// p0345c: loads the backend CAPABILITIES descriptor once per page and caches it
// module-wide — every form in the studio renders its type dropdowns and
// per-type field sets from this single fetch. `null` while loading or on
// failure; the forms degrade to their honest "capabilities unavailable" state
// instead of guessing types client-side.

let cached: ConfigCapabilities | null = null;
let inflight: Promise<ConfigCapabilities> | null = null;

export interface UseCapabilities {
  capabilities: ConfigCapabilities | null;
  error: string | null;
}

export function useCapabilities(): UseCapabilities {
  const [capabilities, setCapabilities] = useState<ConfigCapabilities | null>(cached);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (cached) return;
    let alive = true;
    inflight ??= fetchCapabilities();
    inflight
      .then((caps) => {
        cached = caps;
        if (alive) setCapabilities(caps);
      })
      .catch((err: Error) => {
        inflight = null; // allow a later mount to retry
        if (alive) setError(err.message);
      });
    return () => {
      alive = false;
    };
  }, []);

  return { capabilities, error };
}

/** Test hook: drop the module cache so each test starts cold. */
export function resetCapabilitiesCache(): void {
  cached = null;
  inflight = null;
}
