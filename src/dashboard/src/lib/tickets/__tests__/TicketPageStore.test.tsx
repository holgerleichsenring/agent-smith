import { act, render } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "@/components/tickets/__tests__/ticketFixtures";
import { attempt, conversation, defaults, detail, passes, reviews, series, ticketText } from "@/components/tickets/detail/__tests__/detailFixtures";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { TicketDetail } from "@/types/tickets";

// 2026-10-13-9a59q: the ticket page's one store. Every read is counted by endpoint: a terminal
// attempt's per-run reads happen once for the page's life, a live attempt's follow the refetches, a
// RunsChanged counts only for this ticket's attempts, and the conversation (one tracker read each)
// follows a TicketActivityChanged, a changed attempt status or a SpecCutReviewed — never a bare
// RunsChanged.

const stream = vi.hoisted(() => ({ events: [] as RunEvent[], listeners: new Set<() => void>() }));
vi.mock("@/hooks/useRunEvents", async () => {
  const { useSyncExternalStore } = await import("react");
  return {
    useRunEvents: (runId: string | null) => useSyncExternalStore(
      (l) => { stream.listeners.add(l); return () => stream.listeners.delete(l); },
      () => (runId ? stream.events : EMPTY),
    ),
  };
});
const EMPTY: RunEvent[] = [];

const { TicketPageStore, useAttemptPasses, useTicketSeries } = await import("../TicketPageStore");

function pushEvent(event: RunEvent) {
  stream.events = [...stream.events, event];
  stream.listeners.forEach((l) => l());
}

let current: TicketDetail;
const api = {
  detail: vi.fn(() => Promise.resolve(current)),
  series: vi.fn((_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn((runId: string) => Promise.resolve(passes(runId, {}))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
};

const reads = (fn: { mock: { calls: unknown[][] } }, runId?: string) =>
  fn.mock.calls.filter((c) => runId === undefined || c.includes(runId)).length;

function Shows({ runId }: { runId: string }) {
  useTicketSeries(runId);
  useAttemptPasses(runId);
  return null;
}

async function mount(runId: string | null = null) {
  const { hub, client } = fakeHub();
  const view = render(
    <TicketPageStore project="shop" ticketId="101" client={client} api={api}>
      {runId && <Shows runId={runId} />}
    </TicketPageStore>,
  );
  await settle(0);
  return { hub, view };
}

const settle = (ms: number) => act(() => vi.advanceTimersByTimeAsync(ms));

beforeEach(() => {
  vi.useFakeTimers();
  stream.events = [];
  for (const fn of Object.values(api)) fn.mockClear();
  current = detail([attempt({ runId: "r-1", status: "failed" }), attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null })]);
});
afterEach(() => vi.useRealTimers());

describe("TicketPageStore", () => {
  it("TerminalAttempt_FetchesItsSeriesAndPassesOnceAcrossRemounts_ARefetchRereadsOnlyTheLiveAttempts", async () => {
    const { hub, view } = await mount("r-1");
    view.rerender(<TicketPageStore project="shop" ticketId="101" client={fakeHubClient(hub)} api={api}>{null}</TicketPageStore>);
    view.rerender(<TicketPageStore project="shop" ticketId="101" client={fakeHubClient(hub)} api={api}><Shows runId="r-1" /></TicketPageStore>);
    await settle(0);
    expect(reads(api.series, "r-1")).toBe(1);
    expect(reads(api.passes, "r-1")).toBe(1);

    view.rerender(<TicketPageStore project="shop" ticketId="101" client={fakeHubClient(hub)} api={api}><Shows runId="r-1" /><Shows runId="r-2" /></TicketPageStore>);
    await settle(0);
    act(() => hub.runsChanged.emit("r-2"));
    await settle(2000);
    expect(reads(api.series, "r-1")).toBe(1);
    expect(reads(api.passes, "r-1")).toBe(1);
    expect(reads(api.passes, "r-2")).toBe(2);
  });

  it("WaitingForInputAttempt_IsLive_AndItsPassesAreRefetchedAfterItResumes", async () => {
    current = detail([attempt({ runId: "r-1", status: "waiting_for_input", finishedAt: null })]);
    const { hub } = await mount("r-1");
    expect(reads(api.passes, "r-1")).toBe(1);
    current = detail([attempt({ runId: "r-1", status: "running", finishedAt: null })]);
    act(() => hub.runsChanged.emit("r-1"));
    await settle(2000);
    expect(reads(api.passes, "r-1")).toBe(2);
    current = detail([attempt({ runId: "r-1", status: "success" })]);
    act(() => hub.runsChanged.emit("r-1"));
    await settle(2000);
    await settle(0);
    expect(reads(api.passes, "r-1")).toBeGreaterThanOrEqual(3);
  });

  it("RunsChangedForAnotherTicketsRun_RefetchesNothing_OneForThisTicketsAttempt_RefetchesTheDetailOnceWithin2s", async () => {
    const { hub } = await mount();
    expect(reads(api.detail)).toBe(1);
    act(() => hub.runsChanged.emit("r-other"));
    await settle(5000);
    expect(reads(api.detail)).toBe(1);
    for (let i = 0; i < 10; i++) act(() => hub.runsChanged.emit("r-2"));
    await settle(1999);
    expect(reads(api.detail)).toBe(1);
    await settle(1);
    expect(reads(api.detail)).toBe(2);
    expect(reads(api.series)).toBe(2);
  });

  it("TicketPageStore_SpecReviewRound_RereadsReviewsOnce", async () => {
    await mount();
    expect(reads(api.reviews)).toBe(1);
    act(() => pushEvent({ runId: "r-2", type: EventType.SpecReviewRound, timestamp: "t", scope: "spec", round: 1, reviewer: 0, verdict: "build", cuts: [], findings: [], evidenceProblems: [], residues: [] } as RunEvent));
    act(() => pushEvent({ runId: "r-2", type: EventType.SpecReviewRound, timestamp: "t", scope: "spec", round: 1, reviewer: 1, verdict: "refuse", cuts: [], findings: [], evidenceProblems: [], residues: [] } as RunEvent));
    await settle(2000);
    expect(reads(api.reviews)).toBe(2);
    expect(reads(api.conversation)).toBe(1);
    expect(reads(api.detail)).toBe(1);
  });

  it("TicketPageStore_DefaultDecisionTakenInLiveStream_RereadsDefaultsOnce", async () => {
    await mount();
    expect(reads(api.defaults)).toBe(1);
    const taken = { runId: "r-2", type: EventType.DefaultDecisionTaken, timestamp: "t", question: "q", chosen: "c", alternatives: [], why: "w", holdsSeries: false, kind: "decision" } as RunEvent;
    act(() => pushEvent(taken));
    act(() => pushEvent({ ...taken, question: "q2" } as RunEvent));
    await settle(2000);
    expect(reads(api.defaults)).toBe(2);
    expect(reads(api.reviews)).toBe(1);
    expect(reads(api.detail)).toBe(1);
  });

  it("RunsChangedBurst_IssuesNoConversationRead_AChangedStatusOrASpecCutReviewedIssuesOne", async () => {
    const { hub } = await mount();
    expect(reads(api.conversation)).toBe(1);
    for (let i = 0; i < 5; i++) {
      act(() => hub.runsChanged.emit("r-2"));
      await settle(2000);
    }
    expect(reads(api.detail)).toBe(6);
    expect(reads(api.conversation)).toBe(1);

    current = detail([attempt({ runId: "r-1", status: "failed" }), attempt({ number: 2, runId: "r-2", status: "waiting_for_input", finishedAt: null })]);
    act(() => hub.runsChanged.emit("r-2"));
    await settle(2000);
    await settle(2000);
    expect(reads(api.conversation)).toBe(2);

    act(() => pushEvent({ runId: "r-2", type: EventType.SpecCutReviewed, timestamp: "t", attempt: 1, deliverable: "refused", findings: [] } as RunEvent));
    await settle(2000);
    expect(reads(api.conversation)).toBe(3);
  });

  it("RunsChangedBurst_IssuesNoTicketTextRead_ATicketActivityChangedIssuesOne_DisposeCancelsAPendingOne", async () => {
    const { hub, view } = await mount();
    expect(reads(api.text)).toBe(1);
    current = detail([attempt({ runId: "r-1", status: "failed" }), attempt({ number: 2, runId: "r-2", status: "success" })]);
    for (let i = 0; i < 5; i++) {
      act(() => hub.runsChanged.emit("r-2"));
      await settle(2000);
    }
    expect(reads(api.detail)).toBe(6);
    expect(reads(api.text)).toBe(1);

    for (let i = 0; i < 3; i++) act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await settle(2000);
    expect(reads(api.text)).toBe(2);

    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    view.unmount();
    await settle(3000);
    expect(reads(api.text)).toBe(2);
  });

  it("TicketActivityChanged_ForThisTicket_RereadsEverything_ForAnotherTicket_Nothing", async () => {
    const { hub } = await mount();
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-999" }));
    await settle(3000);
    expect(reads(api.detail)).toBe(1);
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await settle(2000);
    expect(reads(api.detail)).toBe(2);
    expect(reads(api.conversation)).toBe(2);
  });
});

function fakeHubClient(hub: ReturnType<typeof fakeHub>["hub"]) {
  return hub as unknown as Parameters<typeof TicketPageStore>[0]["client"];
}
