"use client";

import { useEffect, useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, JobsHubClient } from "@/lib/JobsHubClient";
import { isSilentReturnFrame } from "@/lib/auth/silentReturnFrame";
import { refusalIn, type ApiRefusal } from "@/lib/apiResponse";
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

/** The one hub URL every subscriber connects through. */
export const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

// p0200: dashboard-side cap of the Recent list. The backend retains 50 for
// debug; the dashboard shows the latest 20 unless URL has ?debug=1.
const RECENT_CAP_DEFAULT = 20;
const RECENT_CAP_DEBUG = 50;

// p0246f: cap the run-list refetch at one per this window. A busy run nudges
// many times/sec; coalescing collapses the burst into a single /api/runs call
// (~3/sec worst case) instead of a storm of mutually-cancelling requests.
const NUDGE_COALESCE_MS = 350;

interface RunList {
  active: RunSnapshot[];
  recent: RunSnapshot[];
}

export interface UseJobsHubResult {
  client: JobsHubClient;
  connectionState: HubConnectionState;
  overview: OverviewSnapshot | null;
  /**
   * 2026-09-21-291b: why this caller is seeing nothing, when the reason is that the
   * server refused them. The hub cannot answer it — SignalR catches the HttpError that
   * carries the status and rejects start() with a FailedToNegotiateWithServerError whose
   * only evidence is prose — but the run fetch beside it already rejects with a typed
   * ApiRefusal, and this hook used to discard it in a bare catch under a comment claiming
   * the connection state surfaced it. Null when nothing has been refused.
   */
  refusal: ApiRefusal | null;
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
  const [refusal, setRefusal] = useState<ApiRefusal | null>(null);

  useEffect(() => {
    // 2026-08-28-0f46: a silent sign-in loads this whole application into a
    // hidden frame that lives about a second. A second live hub connection from
    // that document joins the groups the tab above already holds and then closes
    // them again, and everything it fetches the tab has already fetched.
    if (isSilentReturnFrame()) return;
    let cancelled = false;
    let inFlight: AbortController | null = null;
    let coalesceTimer: ReturnType<typeof setTimeout> | null = null;

    const fetchNow = () => {
      inFlight?.abort();
      const ctrl = new AbortController();
      inFlight = ctrl;
      fetchRuns(ctrl.signal)
        .then((r) => {
          if (cancelled) return;
          setRuns({ active: r.active, recent: r.recent });
          // A fetch that landed is the only proof the refusal is over. Clearing it
          // anywhere else would clear it on the abort that every nudge issues.
          setRefusal(null);
        })
        .catch((thrown: unknown) => {
          const refused = refusalIn(thrown);
          if (!cancelled && refused !== null) setRefusal(refused);
        });
    };

    // p0246f: a live run with N sandboxes emits many events/sec, and the backend
    // fires a RunsChanged nudge per event. Refetching on every nudge would storm
    // /api/runs (each request aborting the last → a wall of (canceled) calls).
    // COALESCE: the first nudge schedules a refetch NUDGE_COALESCE_MS out; further
    // nudges inside that window fold into it. At most one refetch per window, and
    // it always fires within the window of activity (throttle, not trailing
    // debounce — a continuous event stream still updates the UI steadily).
    const scheduleRefetch = () => {
      if (coalesceTimer !== null) return;
      coalesceTimer = setTimeout(() => {
        coalesceTimer = null;
        if (!cancelled) fetchNow();
      }, NUDGE_COALESCE_MS);
    };

    const offConn = client.connectionState.add((state) => {
      setConnectionState(state);
      // Reconnect (or first connect) can have missed nudges — resync the list now.
      if (state === HubConnectionState.Connected) fetchNow();
    });
    const offActivity = client.systemActivityUpdates.add(setSystemActivity);
    const offNudge = client.runsChanged.add(() => scheduleRefetch());

    let cancelOverview: (() => Promise<void>) | null = null;
    client.subscribeOverview().then((cancel) => {
      if (cancelled) cancel();
      else cancelOverview = cancel;
    }).catch(() => { /* connection state surfaces the error */ });

    // Initial paint — don't wait for the first nudge.
    fetchNow();

    return () => {
      cancelled = true;
      if (coalesceTimer !== null) clearTimeout(coalesceTimer);
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

  return { client, connectionState, overview, systemActivity, refusal };
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
  // 2026-08-25-39ab: a snapshot without repos or without a status is not a
  // zombie — it is a snapshot the server answered without those fields, and the
  // filter reads them as absent instead of dereferencing them.
  if ((snapshot.repos ?? []).length > 0) return false;
  if (snapshot.totalSteps > 0 || snapshot.stepIndex > 0) return false;
  return (snapshot.status ?? "").toLowerCase() === "running";
}

export function applySnapshotFilters(snapshot: OverviewSnapshot, debug: boolean): OverviewSnapshot {
  const cap = debug ? RECENT_CAP_DEBUG : RECENT_CAP_DEFAULT;
  const active = debug ? snapshot.active : snapshot.active.filter((r) => !isPreSpawnZombie(r));
  const recent = snapshot.recent.slice(0, cap);
  return { active, recent, systemActivity: snapshot.systemActivity };
}
