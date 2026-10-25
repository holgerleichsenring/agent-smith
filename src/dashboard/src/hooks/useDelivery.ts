"use client";

import { useEffect, useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, type JobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import { CoalescedRefetch } from "@/lib/tickets/CoalescedRefetch";
import { fetchDelivery, type DeliveryPeriod, type DeliveryView } from "@/lib/deliveryApi";

// 2026-10-25-0099c: the Delivery page's one read — on mount and whenever the period or the chosen projects change.
// The answer is a whole-window aggregate over up to 60 days of runs, and RunsChanged fires on every step and LLM
// call, so a RunsChanged waits 15 s; a TicketActivityChanged (a merge, a landing) 2 s. A failed read keeps the
// last answer.

export const DELIVERY_TICKET_NUDGE_MS = 2000;
export const DELIVERY_RUNS_NUDGE_MS = 15000;

export interface DeliveryRead {
  view: DeliveryView | null;
  failed: boolean;
}

export function useDelivery(period: DeliveryPeriod, projects: readonly string[], client?: JobsHubClient): DeliveryRead {
  const hub = useMemo(() => client ?? getJobsHubClient(HUB_URL), [client]);
  const [read, setRead] = useState<DeliveryRead>({ view: null, failed: false });
  const key = projects.join("\n");

  useEffect(() => {
    const chosen = key === "" ? [] : key.split("\n");
    let cancelled = false;
    let inFlight: AbortController | null = null;
    const fetchNow = () => {
      inFlight?.abort();
      const ctrl = new AbortController();
      inFlight = ctrl;
      fetchDelivery(period, chosen, ctrl.signal)
        .then((view) => { if (!cancelled) setRead({ view, failed: false }); })
        .catch(() => { if (!cancelled && !ctrl.signal.aborted) setRead((r) => ({ ...r, failed: true })); });
    };
    const refetch = new CoalescedRefetch(fetchNow);
    const offTicket = hub.ticketActivityChanged.add(() => refetch.within(DELIVERY_TICKET_NUDGE_MS));
    const offRuns = hub.runsChanged.add(() => refetch.within(DELIVERY_RUNS_NUDGE_MS));
    const offConn = hub.connectionState.add((s) => { if (s === HubConnectionState.Connected) fetchNow(); });
    let leave: (() => Promise<void>) | null = null;
    hub.subscribeOverview()
      .then((cancel) => (cancelled ? void cancel() : (leave = cancel)))
      .catch(() => { /* the connection state surfaces it */ });
    fetchNow();
    return () => {
      cancelled = true;
      refetch.cancel();
      inFlight?.abort();
      offTicket();
      offRuns();
      offConn();
      leave?.();
    };
  }, [hub, period, key]);

  return read;
}
