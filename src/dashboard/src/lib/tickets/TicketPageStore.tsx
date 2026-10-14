"use client";

import { createContext, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { getJobsHubClient, type JobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { isRunLive } from "@/lib/runLiveness";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type {
  RunCompute, RunPasses, TicketApprovalPreview, TicketAttempt, TicketConversation, TicketDefaults, TicketDetail, TicketReviews, TicketSeries,
  TicketText,
} from "@/types/tickets";
import {
  TicketPageReads, type PerRunRead, type Read, type TicketPageApi, type TicketPageSnapshot,
} from "./TicketPageReads";
import { useApprovalPreview } from "./useApprovalPreview";

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
  /** 2026-10-13-9a59r: counts this ticket's TicketActivityChanged nudges (and reconnects). */
  activity: number;
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

  const [activity, setActivity] = useState(0);
  const ticketKey = useRef<string | null>(null);
  ticketKey.current = snapshot?.detail.data?.summary.ticketKey ?? null;

  useEffect(() => {
    const offTicket = hub.ticketActivityChanged.add((nudge) => {
      const ours = nudge.ticketKey === ticketKey.current || nudge.ticketKey === ticketId;
      if (nudge.project !== project || !ours) return;
      reads.ticketChanged();
      setActivity((n) => n + 1);
    });
    const offRuns = hub.runsChanged.add((runId) => reads.runChanged(runId));
    const offConn = hub.connectionState.add((state) => {
      if (state !== HubConnectionState.Connected) return;
      reads.ticketChanged();
      setActivity((n) => n + 1);
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
  useNewEvents(liveEvents, EventType.SpecCutReviewed, reads, "cutReviewed");
  useNewEvents(liveEvents, EventType.SpecReviewRound, reads, "reviewRound"); // 2026-10-13-8762l
  useNewEvents(liveEvents, EventType.DefaultDecisionTaken, reads, "defaultTaken"); // 2026-10-13-8762q

  const value = useMemo<StoreValue>(
    () => ({ project, ticketId, snapshot: current, reads, liveAttempt, liveEvents, activity }),
    [project, ticketId, current, reads, liveAttempt, liveEvents, activity],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

/** (d): each SpecCutReviewed the live stream gains re-reads the conversation once; (e) each SpecReviewRound the
 *  review rounds; (f) each DefaultDecisionTaken the defaults. */
function useNewEvents(events: RunEvent[], type: EventType, reads: TicketPageReads, nudge: "cutReviewed" | "reviewRound" | "defaultTaken"): void {
  const seen = useRef<number | null>(null);
  const count = events.filter((e) => e.type === type).length;
  useEffect(() => {
    if (seen.current !== null && count > seen.current) reads[nudge]();
    seen.current = count;
  }, [count, reads, nudge]);
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

/** 2026-10-13-8762l: every attempt's review rounds and the approved set's conversation rounds. */
export function useTicketReviews(): Read<TicketReviews> {
  return useStore().snapshot.reviews;
}

/** 2026-10-13-8762q: the ticket's defaults, pending ones first; changed() re-reads them after this page's own verdict. */
export function useTicketDefaults(): Read<TicketDefaults> & { changed: () => void } {
  const { snapshot, reads } = useStore();
  return { ...snapshot.defaults, changed: () => reads.defaultsChanged() };
}

/** The ticket itself — description, criteria and fields — read on its own tick, never on a RunsChanged. */
export function useTicketText(): Read<TicketText> {
  return useStore().snapshot.text;
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

/** 2026-10-13-9a59r: the per-run reads of several attempts at once — the same store entries as
 *  useTicketSeries(runId) and useAttemptPasses(runId), wanted together for a table of attempts. */
export function useAttemptReads(runIds: readonly string[]): {
  series: Record<string, Read<TicketSeries>>;
  passes: Record<string, Read<RunPasses>>;
} {
  const { snapshot, reads } = useStore();
  const key = runIds.join(",");
  useEffect(() => {
    for (const runId of key ? key.split(",") : []) {
      reads.want("series", runId);
      reads.want("passes", runId);
    }
  }, [reads, key]);
  const series: Record<string, Read<TicketSeries>> = {};
  const passes: Record<string, Read<RunPasses>> = {};
  for (const runId of runIds) {
    series[runId] = (snapshot.runSeries[runId] as Read<TicketSeries> | undefined) ?? PENDING_READ;
    passes[runId] = (snapshot.runPasses[runId] as Read<RunPasses> | undefined) ?? PENDING_READ;
  }
  return { series, passes };
}

/** 2026-10-13-9a59s: one attempt's run detail — its cost cap, LLM call count and reserved Gi-minutes —
 *  through runsApi.fetchRun, held in the page's store like every other per-run read. */
export function useAttemptRun(runId: string | null): Read<RunSnapshot> {
  const { snapshot, reads } = useStore();
  useWanted(reads, ["run"], runId ? [runId] : []);
  if (!runId) return EMPTY_READ;
  return (snapshot.runRun[runId] as Read<RunSnapshot> | undefined) ?? PENDING_READ;
}

/** 2026-10-13-9a59s: one attempt's compute record — spawns, uncounted sandboxes, segments and parks. */
export function useAttemptCompute(runId: string | null): Read<RunCompute> {
  const { snapshot, reads } = useStore();
  useWanted(reads, ["compute"], runId ? [runId] : []);
  if (!runId) return EMPTY_READ;
  return (snapshot.runCompute[runId] as Read<RunCompute> | undefined) ?? PENDING_READ;
}

export interface AttemptMachines {
  run: Record<string, Read<RunSnapshot>>;
  compute: Record<string, Read<RunCompute>>;
  passes: Record<string, Read<RunPasses>>;
}

/** 2026-10-13-9a59s: the run detail, compute and passes reads of several attempts at once — the same
 *  store entries as useAttemptRun, useAttemptCompute and useAttemptPasses — for the tabs that put every
 *  attempt on one scale. */
export function useAttemptMachines(runIds: readonly string[]): AttemptMachines {
  const { snapshot, reads } = useStore();
  useWanted(reads, ["run", "compute", "passes"], runIds);
  const machines: AttemptMachines = { run: {}, compute: {}, passes: {} };
  for (const runId of runIds) {
    machines.run[runId] = (snapshot.runRun[runId] as Read<RunSnapshot> | undefined) ?? PENDING_READ;
    machines.compute[runId] = (snapshot.runCompute[runId] as Read<RunCompute> | undefined) ?? PENDING_READ;
    machines.passes[runId] = (snapshot.runPasses[runId] as Read<RunPasses> | undefined) ?? PENDING_READ;
  }
  return machines;
}

function useWanted(reads: TicketPageReads, kinds: readonly PerRunRead[], runIds: readonly string[]): void {
  const key = runIds.join(",");
  const kindKey = kinds.join(",");
  useEffect(() => {
    for (const runId of key ? key.split(",") : []) {
      for (const kind of kindKey.split(",")) reads.want(kind as PerRunRead, runId);
    }
  }, [reads, key, kindKey]);
}

/** 2026-10-13-9a59r: the approval preview of this ticket — asked only while the conversation shows an
 *  open proposal it does not hide, again on this ticket's TicketActivityChanged and when the newest
 *  attempt's status moves; never on a bare RunsChanged. */
export function useTicketApprovalPreview(): Read<TicketApprovalPreview> {
  const { project, ticketId, snapshot, activity } = useStore();
  const conversation = snapshot.conversation.data;
  const enabled = conversation !== null && conversation.proposal !== null && !conversation.transcriptHidden;
  const attempts = snapshot.detail.data?.attempts ?? [];
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  return useApprovalPreview(project, ticketId, {
    enabled,
    nudge: `${activity}|${newest?.runId ?? ""}=${newest?.status ?? ""}`,
  });
}

const PENDING_READ: Read<never> = { data: null, failed: false, refusal: null, loading: true };
const EMPTY_READ: Read<never> = { data: null, failed: false, refusal: null, loading: false };
