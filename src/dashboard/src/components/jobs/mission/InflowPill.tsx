"use client";

import { useEffect, useMemo, useState } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity } from "@/hooks/useSubsystemActivity";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { cn } from "@/lib/utils";

// p0343b: the mock's INFLOW pill on the runs-home header row. Everything on it
// is real: "last pickup" is the NEWEST run in the same merged list the sections
// render (ticketId + startedAt — the ticket ref renders only when the snapshot
// carries one), and the dot's liveness is the tracker subsystem's freshness
// (the same derivation the rail uses). No runs at all → no pill.
// p0343c: emits the mock's .inflow DOM verbatim.

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
    <div className="inflow" data-testid="inflow-pill" data-live={live ? "true" : "false"}>
      <span className={cn("id", !live && "idle")} />
      {live ? "inflow live" : "inflow idle"} · last pickup{" "}
      <b data-testid="inflow-pill-pickup">
        {newest.ticketId ? `#${newest.ticketId} · ` : ""}
        {relativeAgo(newest.startedAt, nowMs)}
      </b>
    </div>
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
