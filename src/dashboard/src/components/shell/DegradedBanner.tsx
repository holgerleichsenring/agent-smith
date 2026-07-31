"use client";

import { useEffect, useState } from "react";
import { fetchFindings, type StartupFindings } from "@/lib/findingsApi";

const POLL_INTERVAL_MS = 30_000;

/**
 * p0391a: names what is down and why, above every route. The server no longer refuses to
 * start on a broken dependency or a broken trigger, so "it came up" stopped meaning "it is
 * fine" — without this the degraded state is only visible in container logs.
 */
export function DegradedBanner() {
  const findings = useFindings();
  if (findings === null || !findings.degraded) return null;

  const blocking = findings.findings.filter((f) => f.severity === "blocking");
  return (
    <aside
      role="alert"
      data-testid="degraded-banner"
      className="border-b border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900"
    >
      <p className="font-medium">
        Running degraded — {blocking.length} blocking finding
        {blocking.length === 1 ? "" : "s"}. Everything not named below still runs.
      </p>
      <ul className="mt-2 space-y-1">
        {blocking.map((f, i) => (
          <li key={`${f.subsystem}-${f.project ?? ""}-${f.trigger ?? ""}-${i}`}>
            <span className="font-mono text-xs">{unitOf(f.subsystem, f.project, f.trigger)}</span>{" "}
            {f.reason}
          </li>
        ))}
      </ul>
    </aside>
  );
}

function unitOf(subsystem: string, project: string | null, trigger: string | null): string {
  return [subsystem, project, trigger].filter(Boolean).join(" / ");
}

function useFindings(): StartupFindings | null {
  const [findings, setFindings] = useState<StartupFindings | null>(null);

  useEffect(() => {
    let cancelled = false;
    const controller = new AbortController();
    // A findings endpoint that cannot be reached says nothing — the banner reports the
    // server's own account of itself, and has none to show when it cannot get one.
    const load = async () => {
      try {
        const next = await fetchFindings(controller.signal);
        if (!cancelled) setFindings(next);
      } catch {
        if (!cancelled) setFindings(null);
      }
    };
    void load();
    const timer = setInterval(() => void load(), POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      controller.abort();
      clearInterval(timer);
    };
  }, []);

  return findings;
}
