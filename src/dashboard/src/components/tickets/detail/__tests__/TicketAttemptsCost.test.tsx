import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import {
  attempt, compute, conversation, defaults, detail, entry, pass, passes, reviews, runDetail, segment, series, spec, ticketText,
} from "./detailFixtures";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import type { RunCompute, RunPasses, TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";

// 2026-10-13-9a59s: the Attempts and Cost & compute tabs over the page's store — order, the live card, the
// per-run reads' refetch rules, the cross-links, and what the cost bars and compute tiles say.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const nav = vi.hoisted(() => ({ search: "tab=attempts", replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(nav.search),
  useRouter: () => ({ replace: nav.replace, push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const world = {
  detail: null as unknown as TicketDetail,
  series: {} as Record<string, TicketSeries>,
  passes: {} as Record<string, RunPasses>,
  runs: {} as Record<string, RunSnapshot>,
  compute: {} as Record<string, RunCompute>,
  conversation: conversation([]) as TicketConversation,
};
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn((_p: string, _t: string, runId: string | null) => Promise.resolve(world.series[runId ?? "default"])),
  conversation: vi.fn(() => Promise.resolve(world.conversation)),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn((runId: string) => Promise.resolve(world.passes[runId] ?? passes(runId, {}, 0))),
  run: vi.fn((runId: string) => Promise.resolve(world.runs[runId] ?? runDetail(runId))),
  compute: vi.fn((runId: string) => Promise.resolve(world.compute[runId] ?? compute(runId))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
};

const settle = async () => {
  for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(0));
};

async function open() {
  const fake = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={fake.client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
  return fake.hub;
}

const calls = (fn: { mock: { calls: unknown[][] } }, runId: string) => fn.mock.calls.filter((c) => c.includes(runId)).length;

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-09-04T20:00:00Z"));
  nav.search = "tab=attempts";
  nav.replace.mockClear();
  Object.values(api).forEach((fn) => fn.mockClear());
  world.detail = detail([
    attempt({ number: 3, runId: "r-3", status: "running", finishedAt: null, cost: 0.41,
      cause: { channel: "pull_request", actor: "m.reviewer", at: "2026-09-04T14:20:00Z", source: { kind: "review", url: "https://github.com/o/r/pull/813", id: "rv-1" } },
      revision: { number: 3, cause: "review", seriesId: "x", carriedIds: ["s1"], approvalAt: null } }),
    attempt({ number: 1, runId: "r-1", status: "failed", handedBack: true, cost: 1.9, cause: { channel: "status_move", actor: "h.operator", at: "2026-09-03T09:00:00Z", source: { kind: "status", detail: "Ready" } } }),
    attempt({ number: 2, runId: "r-2", status: "success", cost: 3.05,
      cause: { channel: "status_move", actor: "h.operator", at: "2026-09-04T08:02:00Z", source: { kind: "status", detail: "Ready" } },
      executedSpecs: [{ specId: "s1", ordinal: 1, title: "s1", status: "done", verdict: null }] }),
  ]);
  world.series = {
    default: series("r-2", [spec({ specId: "s1", ordinal: 1, state: "done" }), spec({ specId: "s2", ordinal: 2, state: "not_started" })], { attempt: 2 }),
    "r-1": series("r-1", [], { handback: { case: "NotImplementable", runId: "r-1", attempt: 1, at: "2026-09-03T09:10:00Z" } }),
  };
  world.passes = { "r-2": passes("r-2", { s1: [pass({ costUsd: 2 })] }, 0.9), "r-3": passes("r-3", {}, 0.22) };
  world.runs = { "r-2": runDetail("r-2", { llmCalls: 41, budgetCapUsd: 15, reservedGiMinutes: 120 }), "r-3": runDetail("r-3", { reservedGiMinutes: 600 }) };
  world.compute = {};
  world.conversation = conversation([]);
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("Ticket attempts tab", () => {
  it("AttemptsAreNewestFirst_OrderedByRunId_TheLiveOneExpanded", async () => {
    await open();
    const cards = screen.getAllByTestId(/^attempt-\d$/).map((c) => c.getAttribute("data-testid"));
    expect(cards).toEqual(["attempt-3", "attempt-2", "attempt-1"]);
    expect(screen.getByTestId("attempt-head-3")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("attempt-body-3")).toBeVisible();
    expect(screen.getByTestId("attempt-head-2")).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(screen.getByTestId("attempt-head-2"));
    expect(screen.getByTestId("attempt-head-2")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("ticket-tab-attempts")).toHaveTextContent("Attempts3");
    expect(within(screen.getByTestId("attempt-1")).getByTestId("attempt-ended")).toHaveTextContent("Handed back as not implementable.");
  });

  it("TheLiveAttempt_ShowsDeriving_WhileTheSeriesReadsCutCameFromAnEarlierAttempt", async () => {
    await open();
    expect(within(screen.getByTestId("attempt-3")).getByTestId("attempt-specs")).toHaveTextContent("deriving");
    expect(within(screen.getByTestId("attempt-3")).getByTestId("attempt-doing")).toHaveTextContent("Deriving the series");
    cleanup();
    world.series.default = series("r-3", [spec({ specId: "s1", state: "carried" }), spec({ specId: "s2", ordinal: 2, state: "in_progress" })], { attempt: 3 });
    await open();
    expect(within(screen.getByTestId("attempt-3")).getByTestId("attempt-specs")).toHaveTextContent("spec 2 of 2");
  });

  it("ATerminalAttemptsReads_AreFetchedOnce_AWaitingForInputAttemptsAreRefetchedOnANudge", async () => {
    world.detail = detail([
      attempt({ number: 1, runId: "r-1", status: "success" }),
      attempt({ number: 2, runId: "r-2", status: "waiting_for_input", finishedAt: null }),
    ]);
    const hub = await open();
    for (const fn of [api.run, api.compute, api.passes]) {
      expect(calls(fn, "r-1")).toBe(1);
      expect(calls(fn, "r-2")).toBe(1);
    }
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "101" }));
    act(() => hub.runsChanged.emit("r-2"));
    await act(() => vi.advanceTimersByTimeAsync(2100));
    await settle();
    for (const fn of [api.run, api.compute, api.passes]) {
      expect(calls(fn, "r-1")).toBe(1);
      expect(calls(fn, "r-2")).toBe(2);
    }
  });

  it("Trace_LinksToTheWhyView_AndOpenRunViewerToTheRun", async () => {
    await open();
    fireEvent.click(screen.getByTestId("attempt-head-2"));
    const card = screen.getByTestId("attempt-2");
    expect(within(card).getByTestId("attempt-viewer")).toHaveAttribute("href", "/jobs/r-2");
    expect(within(card).getByTestId("attempt-trace")).toHaveAttribute("href", "/jobs/r-2/why");
    expect(within(card).getByTestId("attempt-trace")).toHaveTextContent("Trace · 41 LLM calls");
  });

  it("ReadOnACommentCausedChange_OpensTheConversationAtThatCause_OnAStatusMoveTheTabOnly", async () => {
    await open();
    const anchored = screen.getByTestId("attempt-change-read-3");
    expect(anchored).toHaveAttribute("href", "/tickets/shop/101?tab=conversation&causeOf=r-3");
    fireEvent.click(anchored);
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?tab=conversation&causeOf=r-3", { scroll: false });
    expect(screen.getByTestId("attempt-change-3")).toHaveTextContent("Pull-request review");
    expect(screen.getByTestId("attempt-change-3")).toHaveTextContent("m.reviewer on #813");
    fireEvent.click(screen.getByTestId("attempt-change-read-2"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?tab=conversation", { scroll: false });
    expect(screen.queryByTestId("attempt-change-1")).toBeNull();
  });
});

describe("Ticket cost and compute tab", () => {
  beforeEach(() => {
    nav.search = "tab=cost";
  });

  it("CarriedSpecs_AppearAtZeroInThePartsText_AndAddNothingToTheBar", async () => {
    await open();
    expect(screen.getByTestId("cost-parts-3")).toHaveTextContent("derive $0.22 · S1 carried $0 · so far");
    expect(screen.getByTestId("cost-bar-3").querySelectorAll("span")).toHaveLength(1);
    expect(screen.getByTestId("cost-parts-2")).toHaveTextContent("derive $0.90 · S1 $2.00");
    expect(screen.getByTestId("cost-scale")).toHaveTextContent("the $15.00 run cap is the full width");
    expect(screen.getByTestId("ticket-tab-cost")).toHaveTextContent("$5.36");
    expect(screen.queryByTestId("cost-derivations")).toBeNull();
  });

  it("SpecDerivations_HaveTheirOwnLine_AndTheTotalAndCounterIncludeThem", async () => {
    world.detail = {
      ...world.detail,
      derivations: [
        { runId: "d-2", status: "running", startedAt: "2026-09-04T19:00:00Z", finishedAt: null, cost: 0.5, outcome: "running", specCount: 0, pullRequests: [], summary: null },
        { runId: "d-1", status: "success", startedAt: "2026-09-03T08:00:00Z", finishedAt: "2026-09-03T08:05:00Z", cost: 1.25, outcome: "derived", specCount: 2, pullRequests: [], summary: null },
      ],
    };
    await open();
    expect(screen.getByTestId("cost-derivations")).toHaveTextContent("Spec derivations: 2, not attempts · so far");
    expect(screen.getByTestId("cost-derivations")).toHaveTextContent("$1.75");
    expect(screen.queryByTestId("cost-row-4")).toBeNull();
    expect(screen.getByTestId("cost-total")).toHaveTextContent("$7.11");
    expect(screen.getByTestId("ticket-tab-cost")).toHaveTextContent("$7.11");
  });

  it("TheWaitingBanner_AppearsOnlyWhenWaitingExceededModelCallTime", async () => {
    const parked = segment({
      startedAt: "2026-09-04T08:02:00Z", endedAt: "2026-09-04T08:10:00Z", endStatus: "waiting_for_input", modelMs: 60000,
      park: { kind: "question", askedAt: "2026-09-04T08:09:00Z", questionId: "1", answeredAt: "2026-09-04T11:21:00Z", answeredBy: "h", resumedAt: "2026-09-04T11:21:00Z" },
    });
    world.compute["r-2"] = compute("r-2", { segments: [parked, segment({ startedAt: "2026-09-04T11:21:00Z", endedAt: "2026-09-04T11:40:00Z", modelMs: 60000 })] });
    await open();
    expect(screen.getByTestId("compute-banner")).toHaveTextContent("a question parked attempt 2 for 3h 12m");
    expect(screen.getByTestId("compute-waiting")).toHaveClass("hot");
    cleanup();
    world.compute["r-2"] = compute("r-2", { segments: [{ ...parked, modelMs: 4 * 3600000 }] });
    await open();
    expect(screen.queryByTestId("compute-banner")).toBeNull();
  });

  it("MemoryReserved_IsLabelledAsAReservation_AndLeavesOutLiveAttempts", async () => {
    await open();
    const tile = screen.getByTestId("compute-reserved");
    expect(tile).toHaveTextContent("4.0 Gi·h");
    expect(tile).toHaveTextContent("a reservation · finished attempts only");
  });

  it("AnAttemptWithNoSandboxRecords_ShowsItsRowWithoutPods_RatherThanZero", async () => {
    world.compute["r-1"] = compute("r-1", { spawns: [] });
    await open();
    const row = screen.getByTestId("compute-row-1");
    expect(within(row).getByTestId("compute-pods-1")).toHaveTextContent("no sandbox recorded");
    expect(row.querySelectorAll(".c-n")[0]).toHaveTextContent("—");
    expect(row.querySelectorAll(".c-n")[1]).toHaveTextContent("—");
    expect(within(screen.getByTestId("compute-row-2")).getByTestId("compute-pods-2")).toHaveTextContent("portal-api · 29m · released at run end");
  });
});

describe("Ticket attempts — a cut review on the Did list", () => {
  it("ACutReviewEntry_OfThisAttempt_IsListedWithItsVerdict", async () => {
    world.conversation = conversation([
      entry({ id: "c", source: "spec-cut", kind: "cut-review", attemptRunId: "r-2", detail: { attempt: 1, deliverable: "accepted", findings: [] } }),
    ]);
    await open();
    fireEvent.click(screen.getByTestId("attempt-head-2"));
    expect(within(screen.getByTestId("attempt-2")).getByTestId("attempt-did")).toHaveTextContent("Cut 1: accepted by the reviewer");
  });
});
