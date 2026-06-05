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

// p0200: dashboard-side cap of the Recent list. The backend retains 50 for
// debug; the dashboard shows the latest 20 unless URL has ?debug=1.
const RECENT_CAP_DEFAULT = 20;
const RECENT_CAP_DEBUG = 50;

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
    const debug = isDebugMode();
    const offConn = client.connectionState.add(setConnectionState);
    // p0233: the client now folds JobUpserted into overviewSnapshots and
    // re-emits the whole (authoritative) snapshot, so this single handler
    // covers both the initial snapshot and every live upsert — and the
    // behavior subject replays the CURRENT list to a remounting consumer.
    const offSnap = client.overviewSnapshots.add((snapshot) => {
      setOverview(applySnapshotFilters(snapshot, debug));
      if (snapshot.systemActivity) setSystemActivity(snapshot.systemActivity);
    });
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
      offActivity();
      cancelOverview?.();
    };
  }, [client]);

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

function applySnapshotFilters(snapshot: OverviewSnapshot, debug: boolean): OverviewSnapshot {
  const cap = debug ? RECENT_CAP_DEBUG : RECENT_CAP_DEFAULT;
  const active = debug ? snapshot.active : snapshot.active.filter((r) => !isPreSpawnZombie(r));
  const recent = snapshot.recent.slice(0, cap);
  return { active, recent, systemActivity: snapshot.systemActivity };
}

