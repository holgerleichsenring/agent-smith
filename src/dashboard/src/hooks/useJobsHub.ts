"use client";

import { useEffect, useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, JobsHubClient } from "@/lib/JobsHubClient";
import type {
  OverviewSnapshot,
  RunSnapshot,
  SystemActivitySnapshot,
} from "@/types/hub-events";

// p0169f: ambient hook for the dashboard. Wraps the singleton JobsHubClient
// + tracks connection state + overview snapshot (active + recent). The
// per-run / per-sandbox event streams live in dedicated hooks so a
// component subscribes only to what it renders.

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

export interface UseJobsHubResult {
  client: JobsHubClient;
  connectionState: HubConnectionState;
  overview: OverviewSnapshot | null;
  /**
   * p0175-fix: server-truth 24h rollup. Arrives on the OverviewSnapshot
   * reply and is refreshed via SystemActivityUpdated after each system
   * event batch. Null only until the first snapshot lands.
   */
  systemActivity: SystemActivitySnapshot | null;
}

export function useJobsHub(): UseJobsHubResult {
  const client = useMemo(() => getJobsHubClient(HUB_URL), []);
  const [connectionState, setConnectionState] = useState<HubConnectionState>(client.state());
  const [overview, setOverview] = useState<OverviewSnapshot | null>(null);
  const [systemActivity, setSystemActivity] = useState<SystemActivitySnapshot | null>(null);

  useEffect(() => {
    const offConn = client.connectionState.add(setConnectionState);
    const offSnap = client.overviewSnapshots.add((snapshot) => {
      setOverview(snapshot);
      if (snapshot.systemActivity) setSystemActivity(snapshot.systemActivity);
    });
    const offUpsert = client.jobUpserts.add((snapshot) =>
      setOverview((current) => applyUpsert(current, snapshot)));
    const offActivity = client.systemActivityUpdates.add((snapshot) =>
      setSystemActivity(snapshot));

    let cancelOverview: (() => Promise<void>) | null = null;
    let cancelled = false;
    client.subscribeOverview().then((cancel) => {
      if (cancelled) cancel();
      else cancelOverview = cancel;
    }).catch(() => { /* connection state surfaces the error */ });

    return () => {
      cancelled = true;
      offConn();
      offSnap();
      offUpsert();
      offActivity();
      cancelOverview?.();
    };
  }, [client]);

  return { client, connectionState, overview, systemActivity };
}

function applyUpsert(current: OverviewSnapshot | null, snapshot: RunSnapshot): OverviewSnapshot {
  const base = current ?? { active: [], recent: [], systemActivity: null };
  // Run lifecycle: running → active list (replace by runId); terminal → recent
  // list (prepend, drop oldest beyond 50). Status comparison is case-
  // insensitive to be tolerant of backend variants.
  const isTerminal = ["success", "failed", "error"].includes(snapshot.status.toLowerCase());
  if (isTerminal) {
    const active = base.active.filter((r) => r.runId !== snapshot.runId);
    const recent = [snapshot, ...base.recent.filter((r) => r.runId !== snapshot.runId)].slice(0, 50);
    return { active, recent, systemActivity: base.systemActivity };
  }
  const idx = base.active.findIndex((r) => r.runId === snapshot.runId);
  const active = idx >= 0
    ? base.active.map((r, i) => i === idx ? snapshot : r)
    : [snapshot, ...base.active];
  return { active, recent: base.recent, systemActivity: base.systemActivity };
}
