"use client";

import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, type JobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import { isSilentReturnFrame } from "@/lib/auth/silentReturnFrame";
import { refusalIn, type ApiRefusal } from "@/lib/apiResponse";
import { fetchTickets } from "@/lib/ticketsApi";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import type { TicketListView } from "@/types/tickets";
import { CoalescedRefetch } from "./CoalescedRefetch";

// 2026-10-13-9a59p: the ONE read of the ticket list that the rail and the list share — the
// unfiltered first page and the counts. The list is computed from up to 5000 runs per request with
// no cache, and RunsChanged fires on every step and LLM call, so reads coalesce by route: a ticket
// nudge within 2 s, RunsChanged within 2 s while a ticket route is shown and within 15 s elsewhere
// (only the rail's counts read it there). Configuration routes show no ticket and fetch nothing.

export const TICKET_NUDGE_MS = 2000;
export const RUNS_NUDGE_ON_TICKETS_MS = 2000;
export const RUNS_NUDGE_ELSEWHERE_MS = 15000;

export interface TicketListState {
  list: TicketListView | null;
  refusal: ApiRefusal | null;
  failed: boolean;
  /** Bumped by every read that landed — a bucket view refetches its own page on it. */
  version: number;
}

const EMPTY: TicketListState = { list: null, refusal: null, failed: false, version: 0 };
const Ctx = createContext<TicketListState>(EMPTY);

export function TicketListProvider({ children, client }: { children: ReactNode; client?: JobsHubClient }) {
  const pathname = usePathname() ?? "/";
  const configMode = pathname.startsWith("/config");
  const onTickets = pathname === TICKETS_PATH || pathname.startsWith(`${TICKETS_PATH}/`);
  const hub = useMemo(() => client ?? getJobsHubClient(HUB_URL), [client]);
  const [state, setState] = useState<TicketListState>(EMPTY);
  const onTicketsRef = useRef(onTickets);
  const refetchRef = useRef<CoalescedRefetch | null>(null);
  onTicketsRef.current = onTickets;

  useEffect(() => {
    if (configMode || isSilentReturnFrame()) return;
    let cancelled = false;
    let inFlight: AbortController | null = null;
    const fetchNow = () => {
      inFlight?.abort();
      const ctrl = new AbortController();
      inFlight = ctrl;
      fetchTickets({}, ctrl.signal)
        .then((list) => {
          if (!cancelled) setState((s) => ({ list, refusal: null, failed: false, version: s.version + 1 }));
        })
        .catch((thrown: unknown) => {
          if (cancelled || ctrl.signal.aborted) return;
          const refusal = refusalIn(thrown);
          setState((s) => ({ ...s, refusal, failed: refusal === null }));
        });
    };
    const refetch = new CoalescedRefetch(fetchNow);
    refetchRef.current = refetch;
    const offTicket = hub.ticketActivityChanged.add(() => refetch.within(TICKET_NUDGE_MS));
    const offRuns = hub.runsChanged.add(() =>
      refetch.within(onTicketsRef.current ? RUNS_NUDGE_ON_TICKETS_MS : RUNS_NUDGE_ELSEWHERE_MS));
    const offConn = hub.connectionState.add((s) => {
      if (s === HubConnectionState.Connected) fetchNow();
    });
    let leave: (() => Promise<void>) | null = null;
    hub.subscribeOverview()
      .then((cancel) => (cancelled ? void cancel() : (leave = cancel)))
      .catch(() => { /* the connection state surfaces it */ });
    fetchNow();
    return () => {
      cancelled = true;
      refetch.cancel();
      refetchRef.current = null;
      inFlight?.abort();
      offTicket();
      offRuns();
      offConn();
      leave?.();
    };
  }, [hub, configMode]);

  // Arriving on a ticket route from elsewhere: a read held for the 15 s window is due now.
  const wasOnTickets = useRef(onTickets);
  useEffect(() => {
    if (onTickets && !wasOnTickets.current) refetchRef.current?.within(0);
    wasOnTickets.current = onTickets;
  }, [onTickets]);

  return <Ctx.Provider value={state}>{children}</Ctx.Provider>;
}

/** The shared ticket list; without a provider, nothing has been read. */
export function useTicketList(): TicketListState {
  return useContext(Ctx);
}
