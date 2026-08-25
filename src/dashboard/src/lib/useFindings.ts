"use client";

import { useEffect, useState } from "react";
import { fetchFindings, type StartupFindings } from "@/lib/findingsApi";

export const FINDINGS_POLL_INTERVAL_MS = 30_000;

/**
 * p0391a: the server's own account of itself, refreshed while the operator watches.
 *
 * 2026-08-25-8c97: extracted from the degraded banner, because the build difference is a
 * second reading of the same document — a finding the same poll already carries, shown
 * with a different action. A findings endpoint that cannot be reached says nothing: this
 * answers null, and every caller renders nothing rather than guessing.
 */
export function useFindings(): StartupFindings | null {
  const [findings, setFindings] = useState<StartupFindings | null>(null);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();
    const load = async () => {
      try {
        const next = await fetchFindings(controller.signal);
        if (!cancelled) setFindings(next);
      } catch {
        if (!cancelled) setFindings(null);
      }
    };
    void load();
    const timer = setInterval(() => void load(), FINDINGS_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      controller.abort();
      clearInterval(timer);
    };
  }, []);

  return findings;
}
