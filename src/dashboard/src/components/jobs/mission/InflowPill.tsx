"use client";

import { useEffect, useMemo, useState } from "react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity } from "@/hooks/useSubsystemActivity";
import { mergeNewestFirst } from "@/lib/runs/mergeNewestFirst";
import { cn } from "@/lib/utils";
import { useT } from "@/hooks/useT";
import { relativeAgo } from "../RunStats";

// p0343b: the mock's INFLOW pill on the runs-home header row. Everything on it
// is real: "last pickup" is the NEWEST run in the same merged list the sections
// render (ticketId + startedAt — the ticket ref renders only when the snapshot
// carries one), and the dot's liveness is the tracker subsystem's freshness
// (the same derivation the rail uses). No runs at all → no pill.
// p0343c: emits the mock's .inflow DOM verbatim.

export function InflowPill() {
  const t = useT();
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
      {live ? t("runs.mission.inflowLive") : t("runs.mission.inflowIdle")} · {t("runs.mission.lastPickup")}{" "}
      <b data-testid="inflow-pill-pickup">
        {newest.ticketId ? `#${newest.ticketId} · ` : ""}
        {relativeAgo(t, newest.startedAt, nowMs)}
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
