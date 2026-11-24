"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { HUB_URL } from "@/hooks/useJobsHub";
import { ApiResponseError } from "@/lib/apiResponse";
import { getJobsHubClient, type JobsHubClient } from "@/lib/JobsHubClient";
import { connectedListener } from "@/lib/tickets/connectedAgain";
import { CoalescedRefetch } from "@/lib/tickets/CoalescedRefetch";
import { fetchSeriesRun } from "@/lib/seriesRunsApi";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-23-31dcd: one series' page reads ONE model, GET /api/series-runs/{id}; a 404 is "no such series, or not
// yours to see" (the server answers both alike), anything else a failed read. 2026-11-23-31dce: reload reads it again
// after an act, keeping the page on screen meanwhile. 2026-11-24-a4e7: and whenever the host moves one of its pull
// requests — a merge that completes, CI that turns, a retarget — which the server says as a TicketActivityChanged of the
// series' project once its records read the change, and after a reconnect, which missed those nudges.

/** A burst of the project's ticket nudges costs one read within this window — a merge, a landing and its dependents'
 *  wake arrive together, and 2 s is the ticket list's and the Delivery page's window for the same nudge. */
export const SERIES_TICKET_NUDGE_MS = 2000;

export type SeriesRunRead =
  | { kind: "loading" }
  | { kind: "notFound" }
  | { kind: "failed" }
  | { kind: "read"; detail: SeriesRunDetail };

export function useSeriesRunDetail(id: string, client?: JobsHubClient): { read: SeriesRunRead; reload: () => void } {
  const [read, setRead] = useState<SeriesRunRead>({ kind: "loading" });
  const [version, setVersion] = useState(0);
  const shown = useRef<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    if (shown.current !== id) setRead({ kind: "loading" });
    fetchSeriesRun(id, controller.signal)
      .then((detail) => {
        if (controller.signal.aborted) return;
        shown.current = id;
        setRead({ kind: "read", detail });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted || shown.current === id) return; // a failed reload keeps the page it had
        setRead(error instanceof ApiResponseError && error.status === 404 ? { kind: "notFound" } : { kind: "failed" });
      });
    return () => controller.abort();
  }, [id, version]);
  const reload = useCallback(() => setVersion((v) => v + 1), []);
  useSeriesNudges(read.kind === "read" ? read.detail.project : null, reload, client);
  return { read, reload };
}

// The series' project's ticket nudges and a reconnect read the series again; the first connect does not, the page's own
// read is newer than it.
function useSeriesNudges(project: string | null, reload: () => void, client?: JobsHubClient): void {
  const hub = useMemo(() => client ?? getJobsHubClient(HUB_URL), [client]);
  useEffect(() => {
    if (!project) return;
    const refetch = new CoalescedRefetch(reload);
    const offTicket = hub.ticketActivityChanged.add((nudge) => {
      if (nudge.project === project) refetch.within(SERIES_TICKET_NUDGE_MS);
    });
    const offConn = hub.connectionState.add(connectedListener(hub.state(), { first: () => {}, again: reload }));
    let leave: (() => Promise<void>) | null = null;
    let left = false;
    hub.subscribeOverview()
      .then((cancel) => (left ? void cancel() : (leave = cancel)))
      .catch(() => { /* the connection state surfaces it */ });
    return () => {
      left = true;
      refetch.cancel();
      offTicket();
      offConn();
      void leave?.();
    };
  }, [hub, project, reload]);
}
