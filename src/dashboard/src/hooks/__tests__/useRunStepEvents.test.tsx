import { act, renderHook } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { useRunStepEvents, STEP_POLL_INTERVAL_MS } from "@/hooks/useRunStepEvents";
import { EventType, type RunEvent, type SandboxCommandEvent } from "@/types/hub-events";

// p0388d: the open step has to FOLLOW the run. Two defects produced the symptom
// the operator reported — the counter on the left climbing while the pane on the
// right showed nothing new, even after a reload: the page was anchored at the
// step's OLDEST row, and nothing ever re-read it.
//
// The fake below is the step-events endpoint's contract, not a stub of the hook:
// it answers a newest-anchored page, a backwards page and a forward delta, each
// with the cursors the real endpoint ships.

const PAGE = 10;
let rows: SandboxCommandEvent[] = [];

function makeRows(count: number, from = 0): SandboxCommandEvent[] {
  return Array.from({ length: count }, (_, i) => ({
    runId: "r1",
    type: EventType.SandboxCommand,
    timestamp: "2026-07-29T09:00:00Z",
    repo: "primary",
    command: "run_command",
    argsLength: 4,
    summary: `cmd-${from + i}`,
  }));
}

/** Rows are stored in Seq order; the index + 1 IS the Seq, so the fake can
 *  answer cursor questions the same way the indexed query does. */
function seqOf(row: SandboxCommandEvent): number {
  return rows.indexOf(row) + 1;
}

function respond(rawUrl: string) {
  const url = new URL(rawUrl, "http://dashboard.test");
  const since = url.searchParams.get("sinceSeq");
  const before = url.searchParams.get("beforeSeq");

  if (since !== null) {
    const after = rows.slice(Number(since));
    const slice = after.slice(0, PAGE);
    return json({
      events: slice,
      newestSeq: slice.length > 0 ? seqOf(slice[slice.length - 1]) : Number(since),
      hasNewer: after.length > PAGE,
    });
  }

  const below = before === null ? rows : rows.slice(0, Number(before) - 1);
  const slice = below.slice(Math.max(0, below.length - PAGE));
  return json({
    events: slice,
    oldestSeq: slice.length > 0 ? seqOf(slice[0]) : Number(before ?? 0),
    newestSeq: slice.length > 0 ? seqOf(slice[slice.length - 1]) : Number(before ?? 0),
    hasOlder: below.length > slice.length,
  });
}

function json(body: unknown) {
  return { ok: true, json: async () => body };
}

function summaries(events: RunEvent[]): (string | null)[] {
  return events.map((e) => (e as SandboxCommandEvent).summary);
}

const fetchMock = vi.fn();

beforeEach(() => {
  // Braced on purpose: a concise arrow RETURNS the mock, and vitest treats a
  // value returned from a hook as its teardown callback.
  rows = [];
  fetchMock.mockReset();
  fetchMock.mockImplementation(async (url: string) => respond(url));
  vi.stubGlobal("fetch", fetchMock);
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

async function settle(ms = 0) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("useRunStepEvents", () => {
  it("StepDetail_OpeningALongStep_ShowsItsNewestRows", async () => {
    rows = makeRows(25);

    const { result } = renderHook(() => useRunStepEvents("r1", 0, false));
    await settle();

    // The step's END, not its beginning — the anchor defect this replaces
    // returned cmd-0…cmd-9 for exactly this step.
    expect(summaries(result.current.events)).toEqual(
      Array.from({ length: PAGE }, (_, i) => `cmd-${15 + i}`));
    expect(result.current.hasOlder).toBe(true);
  });

  it("StepDetail_LoadOlder_WalksIntoHistoryWithoutGaps", async () => {
    rows = makeRows(25);

    const { result } = renderHook(() => useRunStepEvents("r1", 0, false));
    await settle();

    await act(async () => result.current.loadOlder());
    await settle();
    await act(async () => result.current.loadOlder());
    await settle();

    expect(summaries(result.current.events)).toEqual(
      Array.from({ length: 25 }, (_, i) => `cmd-${i}`));
    expect(result.current.hasOlder).toBe(false);
  });

  it("StepDetail_RunLive_PollsForwardAndAppendsNewRows", async () => {
    rows = makeRows(3);

    const { result } = renderHook(() => useRunStepEvents("r1", 0, true));
    await settle();
    expect(result.current.events).toHaveLength(3);

    // The step keeps producing while the pane sits open, and the operator
    // presses nothing.
    rows = [...rows, ...makeRows(2, 3)];
    await settle(STEP_POLL_INTERVAL_MS);

    expect(summaries(result.current.events)).toEqual(
      ["cmd-0", "cmd-1", "cmd-2", "cmd-3", "cmd-4"]);
  });

  it("StepDetail_RunFinished_StopsPolling", async () => {
    rows = makeRows(3);

    const { rerender } = renderHook(
      ({ live }) => useRunStepEvents("r1", 0, live),
      { initialProps: { live: true } },
    );
    await settle();
    await settle(STEP_POLL_INTERVAL_MS);
    expect(fetchMock.mock.calls.length).toBeGreaterThan(1);

    // The run reaches a terminal status while the pane is open.
    rerender({ live: false });
    const afterFinish = fetchMock.mock.calls.length;
    rows = [...rows, ...makeRows(5, 3)];
    await settle(10 * STEP_POLL_INTERVAL_MS);

    // A finished run costs nothing.
    expect(fetchMock.mock.calls.length).toBe(afterFinish);
  });

  it("StepDetail_BurstLargerThanAPage_CatchesUpWithoutWaitingATickPerPage", async () => {
    rows = makeRows(1);

    const { result } = renderHook(() => useRunStepEvents("r1", 0, true));
    await settle();

    // 25 rows arrive between two ticks — three pages' worth. ONE tick brings
    // them all: the pane never trails a busy step by a page a second.
    rows = [...rows, ...makeRows(25, 1)];
    await settle(STEP_POLL_INTERVAL_MS);

    expect(result.current.events).toHaveLength(26);
  });
});
