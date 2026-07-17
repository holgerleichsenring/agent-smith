"use client";

import { useEffect, useMemo, useState } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity } from "@/hooks/useSubsystemActivity";
import { mergeNewestFirst } from "@/components/jobs/RunsList";

// p0343b: the mock's INFLOW pill on the runs-home header row. Everything on it
// is real: "last pickup" is the NEWEST run in the same merged list the sections
// render (ticketId + startedAt — the ticket ref renders only when the snapshot
// carries one), and the dot's liveness is the tracker subsystem's freshness
// (the same derivation the rail uses). No runs at all → no pill.

export function InflowPill() {
  const { overview } = useJobsHub();
  const events = useSystemBacklog();
  const activity = useSubsystemActivity(events);
  const nowMs = useNowTick(30_000);

  const newest = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent)[0] ?? null : null),
    [overview],
  );

  if (!newest) return null;

  const live = activity.tracker.live;
  return (
    <span
      data-testid="inflow-pill"
      data-live={live ? "true" : "false"}
      className="badge-pill gap-2 border border-stone-200 bg-[var(--color-canvas-soft)] font-mono dsh-mono text-stone-500"
    >
      <span
        aria-hidden
        className={`h-2 w-2 flex-none rounded-full ${live ? "animate-pulse bg-emerald-500" : "bg-stone-300"}`}
      />
      {live ? "inflow live" : "inflow idle"}
      <span className="text-stone-300">·</span>
      <span data-testid="inflow-pill-pickup">
        last pickup{newest.ticketId ? ` #${newest.ticketId}` : ""} · {relativeAgo(newest.startedAt, nowMs)}
      </span>
    </span>
  );
}

function useNowTick(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}

function relativeAgo(iso: string, nowMs: number): string {
  const seconds = Math.max(0, Math.round((nowMs - new Date(iso).getTime()) / 1000));
  if (seconds < 45) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.round(hours / 24)}d ago`;
}
