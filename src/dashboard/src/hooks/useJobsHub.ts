"use client";

import { useEffect, useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, JobsHubClient } from "@/lib/JobsHubClient";
import { fetchRuns } from "@/lib/runsApi";
import type {
  OverviewSnapshot,
  RunSnapshot,
  SystemActivitySnapshot,
} from "@/types/hub-events";

// p0169f / p0246f: ambient hook for the dashboard. The run list (active +
// recent) is READ from the DB via GET /api/runs and refetched whenever the hub
// fires a RunsChanged nudge — Redis is transport only, not the source of the
// list. System-activity KPIs are still live over SignalR. The per-run /
// per-sandbox event streams live in dedicated hooks so a component subscribes
// only to what it renders.

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

// p0200: dashboard-side cap of the Recent list. The backend retains 50 for
// debug; the dashboard shows the latest 20 unless URL has ?debug=1.
const RECENT_CAP_DEFAULT = 20;
const RECENT_CAP_DEBUG = 50;

interface RunList {
  active: RunSnapshot[];
  recent: RunSnapshot[];
}

export interface UseJobsHubResult {
  client: JobsHubClient;
  connectionState: HubConnectionState;
  overview: OverviewSnapshot | null;
  /**
   * p0175-fix: server-truth 24h rollup. Pushed on SubscribeOverview and
   * refreshed via SystemActivityUpdated after each system event batch.
   * Null only until the first push lands.
   */
  systemActivity: SystemActivitySnapshot | null;
}

export function useJobsHub(): UseJobsHubResult {
  const client = useMemo(() => getJobsHubClient(HUB_URL), []);
  const [connectionState, setConnectionState] = useState<HubConnectionState>(client.state());
  const [runs, setRuns] = useState<RunList | null>(null);
  const [systemActivity, setSystemActivity] = useState<SystemActivitySnapshot | null>(null);

  useEffect(() => {
    let cancelled = false;
    let inFlight: AbortController | null = null;

    // p0246f: the nudge says "something changed" — refetch the authoritative
    // list from the DB. No client-side fold of run state; the DB is the truth.
    const refetch = () => {
      inFlight?.abort();
      const ctrl = new AbortController();
      inFlight = ctrl;
      fetchRuns(ctrl.signal)
        .then((r) => { if (!cancelled) setRuns({ active: r.active, recent: r.recent }); })
        .catch(() => { /* connection state surfaces errors; next nudge retries */ });
    };

    const offConn = client.connectionState.add((state) => {
      setConnectionState(state);
      // Reconnect (or first connect) can have missed nudges — resync the list.
      if (state === HubConnectionState.Connected) refetch();
    });
    const offActivity = client.systemActivityUpdates.add(setSystemActivity);
    const offNudge = client.runsChanged.add(() => refetch());

    let cancelOverview: (() => Promise<void>) | null = null;
    client.subscribeOverview().then((cancel) => {
      if (cancelled) cancel();
      else cancelOverview = cancel;
    }).catch(() => { /* connection state surfaces the error */ });

    // Initial paint — don't wait for the first nudge.
    refetch();

    return () => {
      cancelled = true;
      inFlight?.abort();
      offConn();
      offActivity();
      offNudge();
      cancelOverview?.();
    };
  }, [client]);

  const overview = useMemo<OverviewSnapshot | null>(() => {
    if (!runs) return null;
    return applySnapshotFilters(
      { active: runs.active, recent: runs.recent, systemActivity },
      isDebugMode(),
    );
  }, [runs, systemActivity]);

  return { client, connectionState, overview, systemActivity };
}

function isDebugMode(): boolean {
  if (typeof window === "undefined") return false;
  return new URLSearchParams(window.location.search).get("debug") === "1";
}

/**
 * p0200: pre-spawn zombie filter. A snapshot with no repos, no step
 * progress, and a non-terminal status is a card that landed before the
 * first real event — operators see these as noise. Hidden by default;
 * surfaces under ?debug=1 for devs.
 */
export function isPreSpawnZombie(snapshot: RunSnapshot): boolean {
  if (snapshot.repos.length > 0) return false;
  if (snapshot.totalSteps > 0 || snapshot.stepIndex > 0) return false;
  return snapshot.status.toLowerCase() === "running";
}

export function applySnapshotFilters(snapshot: OverviewSnapshot, debug: boolean): OverviewSnapshot {
  const cap = debug ? RECENT_CAP_DEBUG : RECENT_CAP_DEFAULT;
  const active = debug ? snapshot.active : snapshot.active.filter((r) => !isPreSpawnZombie(r));
  const recent = snapshot.recent.slice(0, cap);
  return { active, recent, systemActivity: snapshot.systemActivity };
}
