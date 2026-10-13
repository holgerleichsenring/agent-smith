"use client";

import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, type JobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { isRunLive } from "@/lib/runLiveness";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketAttempt, TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";
import { TicketPageReads, type Read, type TicketPageApi, type TicketPageSnapshot } from "./TicketPageReads";

// 2026-10-13-9a59q: the ONE store of a ticket page. Every ticket-level read lives here, so the Summary
// tab and the tabs 9a59r and 9a59s add never issue a second read of one endpoint. Refetch rules: a
// TicketActivityChanged naming this ticket re-reads everything; a RunsChanged for one of its attempts
// re-reads the detail, the default series and the live attempt's reads — never the conversation,
// which reads the tracker and follows a changed attempt status or a SpecCutReviewed instead. A new
// attempt is learned from TicketActivityChanged, never by scanning the overview's runs.

interface StoreValue {
  project: string;
  ticketId: string;
  snapshot: TicketPageSnapshot;
  reads: TicketPageReads;
  liveAttempt: TicketAttempt | null;
  liveEvents: RunEvent[];
}

const Ctx = createContext<StoreValue | null>(null);

interface Props {
  project: string;
  ticketId: string;
  children: ReactNode;
  client?: JobsHubClient;
  api?: TicketPageApi;
}

export function TicketPageStore({ project, ticketId, children, client, api }: Props) {
  const hub = useMemo(() => client ?? getJobsHubClient(HUB_URL), [client]);
  const [snapshot, setSnapshot] = useState<TicketPageSnapshot | null>(null);
  const reads = useMemo(() => new TicketPageReads(project, ticketId, setSnapshot, api), [project, ticketId, api]);

  useEffect(() => {
    reads.start();
    return () => reads.dispose();
  }, [reads]);

  const ticketKey = useRef<string | null>(null);
  ticketKey.current = snapshot?.detail.data?.summary.ticketKey ?? null;

  useEffect(() => {
    const offTicket = hub.ticketActivityChanged.add((nudge) => {
      const ours = nudge.ticketKey === ticketKey.current || nudge.ticketKey === ticketId;
      if (nudge.project === project && ours) reads.ticketChanged();
    });
    const offRuns = hub.runsChanged.add((runId) => reads.runChanged(runId));
    const offConn = hub.connectionState.add((state) => {
      if (state === HubConnectionState.Connected) reads.ticketChanged();
    });
    let leave: (() => Promise<void>) | null = null;
    let left = false;
    hub.subscribeOverview()
      .then((cancel) => (left ? void cancel() : (leave = cancel)))
      .catch(() => { /* the connection state surfaces it */ });
    return () => {
      left = true;
      offTicket();
      offRuns();
      offConn();
      leave?.();
    };
  }, [hub, reads, project, ticketId]);

  const current = snapshot ?? reads.snapshot;
  const liveAttempt = newestLive(current.detail.data);
  const liveEvents = useRunEvents(liveAttempt?.runId ?? null);
  useCutReviews(liveEvents, reads);

  const value = useMemo<StoreValue>(
    () => ({ project, ticketId, snapshot: current, reads, liveAttempt, liveEvents }),
    [project, ticketId, current, reads, liveAttempt, liveEvents],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

/** (d): each SpecCutReviewed the live stream gains re-reads the conversation once. */
function useCutReviews(events: RunEvent[], reads: TicketPageReads): void {
  const seen = useRef<number | null>(null);
  const count = events.filter((e) => e.type === EventType.SpecCutReviewed).length;
  useEffect(() => {
    if (seen.current !== null && count > seen.current) reads.cutReviewed();
    seen.current = count;
  }, [count, reads]);
}

function newestLive(detail: TicketDetail | null): TicketAttempt | null {
  const live = detail?.attempts.filter((a) => isRunLive(a.status)) ?? [];
  return live.length > 0 ? live[live.length - 1] : null;
}

function useStore(): StoreValue {
  const store = useContext(Ctx);
  if (!store) throw new Error("A ticket page read is used outside its TicketPageStore");
  return store;
}

/** The page's ticket: which one, its detail, and the attempt that is live now with its stream. */
export function useTicketDetail(): Read<TicketDetail> & { project: string; ticketId: string } {
  const { project, ticketId, snapshot } = useStore();
  return { ...snapshot.detail, project, ticketId };
}

/** No run id: the ticket's default cut. A run id: that attempt's cut and gate. */
export function useTicketSeries(runId?: string | null): Read<TicketSeries> {
  const { snapshot, reads } = useStore();
  useEffect(() => {
    if (runId) reads.want("series", runId);
  }, [reads, runId]);
  if (!runId) return snapshot.series;
  return (snapshot.runSeries[runId] as Read<TicketSeries> | undefined) ?? PENDING_READ;
}

export function useTicketConversation(): Read<TicketConversation> {
  return useStore().snapshot.conversation;
}

export function useAttemptPasses(runId: string | null): Read<RunPasses> {
  const { snapshot, reads } = useStore();
  useEffect(() => {
    if (runId) reads.want("passes", runId);
  }, [reads, runId]);
  if (!runId) return EMPTY_READ;
  return (snapshot.runPasses[runId] as Read<RunPasses> | undefined) ?? PENDING_READ;
}

/** The live attempt and its event stream (useRunEvents) — null and empty when none is live. */
export function useLiveAttempt(): { attempt: TicketAttempt | null; events: RunEvent[] } {
  const { liveAttempt, liveEvents } = useStore();
  return { attempt: liveAttempt, events: liveEvents };
}

const PENDING_READ: Read<never> = { data: null, failed: false, refusal: null, loading: true };
const EMPTY_READ: Read<never> = { data: null, failed: false, refusal: null, loading: false };
