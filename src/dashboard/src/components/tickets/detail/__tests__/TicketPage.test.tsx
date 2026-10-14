import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub, pr } from "../../__tests__/ticketFixtures";
import { attempt, budget, conversation, defaults, detail, entry, pass, passes, reviews, series, spec, ticketText } from "./detailFixtures";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";

// 2026-10-13-9a59q: the ticket page as a reader meets it — header, Summary cells, the graph of each
// attempt from that attempt's own records, and the node detail — driven through the page's one store.

const stream = vi.hoisted(() => ({ events: [] as RunEvent[], listeners: new Set<() => void>() }));
vi.mock("@/hooks/useRunEvents", async () => {
  const { useSyncExternalStore } = await import("react");
  return {
    useRunEvents: (runId: string | null) => useSyncExternalStore(
      (l) => { stream.listeners.add(l); return () => stream.listeners.delete(l); },
      () => (runId ? stream.events : NONE),
    ),
  };
});
const NONE: RunEvent[] = [];
const nav = vi.hoisted(() => ({ search: "", replace: vi.fn() }));
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
  run: () => Promise.reject(new Error("unused")),
  compute: () => Promise.reject(new Error("unused")),
  conversation: conversation([]) as TicketConversation,
};
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn((_p: string, _t: string, runId: string | null) =>
    Promise.resolve(world.series[runId ?? "default"] ?? series("", [], { runId: null, gate: null, recorded: false }))),
  conversation: vi.fn(() => Promise.resolve(world.conversation)),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn((runId: string) => Promise.resolve(world.passes[runId] ?? passes(runId, {}, 0))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
};

const settle = (ms = 0) => act(() => vi.advanceTimersByTimeAsync(ms));

async function open() {
  const { hub, client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
  await settle();
  return hub;
}

const node = (id: string) => document.querySelector(`[data-node="${id}"]`) as SVGGElement;
const detailText = () => screen.getByTestId("node-detail").textContent ?? "";

beforeEach(() => {
  vi.useFakeTimers();
  stream.events = [];
  nav.search = "";
  for (const fn of Object.values(api)) fn.mockClear();
  world.series = {};
  world.passes = {};
  world.conversation = conversation([]);
  world.detail = detail([attempt()]);
  world.series["r-1"] = series("r-1", [spec({ repos: ["portal-api"] }), spec({ specId: "s2", ordinal: 2, repos: ["portal-worker"] })]);
});
afterEach(() => vi.useRealTimers());

describe("Ticket page", () => {
  it("Header_ShowsOnlyRecordedFields_AndThisAttemptListsTheUnionOfTheCutsRepositories", async () => {
    world.detail = detail([attempt()], { branch: "" });
    await open();
    const head = screen.getByTestId("ticket-head");
    expect(head).toHaveTextContent("GH-101");
    expect(head).toHaveTextContent("Fix the login");
    expect(within(head).queryByRole("link")).toBeNull();
    expect(head).not.toHaveTextContent("tracker status");
    expect(head).not.toHaveTextContent("branch");
    expect(screen.getByTestId("cell-this-attempt")).toHaveTextContent("portal-api, portal-worker · run r-1");
  });

  it("Header_LinksTheTracker_AndNamesTheStatusAtTheNewestAttempt", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, webUrl: "https://example.test/101", trackerState: "Active", platform: "github" };
    await open();
    const head = screen.getByTestId("ticket-head");
    expect(within(head).getByRole("link")).toHaveAttribute("href", "https://example.test/101");
    expect(head).toHaveTextContent("tracker status Active at attempt 1");
    expect(head).toHaveTextContent("branch agent-smith/GH-101");
  });

  it("Header_FailedAttemptWithMergedPullRequests_IsOk_AndNamesBothFates", async () => {
    world.detail = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "done", landing: "merged",
      pullRequests: [pr({ state: "merged" }), pr({ number: "810", state: "merged" })] };
    await open();
    const head = screen.getByTestId("ticket-head");
    expect(head.querySelector(".ticon")?.className).toBe("ticon ok");
    expect(head).toHaveTextContent("attempt 2 failed");
    expect(screen.getByTestId("ticket-pr-fate")).toHaveTextContent("2 PRs merged");
  });

  it("Header_SucceededAttemptWithEveryPullRequestClosedUnmerged_IsBad", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "idle", lastOutcome: "success", landing: "unmerged",
      pullRequests: ["1", "2", "3"].map((number) => pr({ number, state: "closed" })) };
    await open();
    expect(screen.getByTestId("ticket-head").querySelector(".ticon")?.className).toBe("ticon bad");
    expect(screen.getByTestId("ticket-pr-fate")).toHaveTextContent("3 PRs closed unmerged");
  });

  it("Header_OneMergedOneOpen_NamesBoth", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "waiting_on_review",
      pullRequests: [pr({ state: "merged" }), pr({ number: "810", state: "open" })] };
    await open();
    expect(screen.getByTestId("ticket-pr-fate")).toHaveTextContent("1 PR merged, 1 open");
  });

  it("Header_AQueuedWorkingTicket_DrawsTheListRowsQueuedDot", async () => {
    world.detail = detail([attempt({ status: "queued", finishedAt: null })]);
    await open();
    expect(screen.getByTestId("ticket-head").querySelector(".ticon")?.className).toBe("ticon queued");
  });

  it("CancelAttempt_ShowsOnlyForALiveAttempt_AndReadsCancellingAfterARunCancelRequested", async () => {
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null })]);
    await open();
    const cancel = screen.getByTestId("cancel-run-r-2");
    expect(cancel).toHaveTextContent("Cancel attempt 2");
    act(() => {
      stream.events = [{ runId: "r-2", type: EventType.RunCancelRequested, timestamp: "t" } as RunEvent];
      stream.listeners.forEach((l) => l());
    });
    expect(screen.getByTestId("cancel-run-r-2")).toHaveTextContent("cancelling…");
  });

  it("CancelAttempt_IsAbsentWhenNoAttemptIsLive", async () => {
    await open();
    expect(screen.queryByText(/Cancel attempt/)).toBeNull();
  });

  it("Nodes_AreReachableByTab_AndOpenTheirDetailOnEnterAndSpace", async () => {
    await open();
    const gate = node("gate");
    expect(gate).toHaveAttribute("tabindex", "0");
    fireEvent.keyDown(gate, { key: "Enter" });
    expect(screen.getByTestId("node-detail")).toHaveTextContent("Spec gate");
    fireEvent.keyDown(node("s1"), { key: " " });
    expect(screen.getByTestId("node-detail")).toHaveTextContent("Spec 1");
    expect(screen.getByTestId("spec-goal")).toHaveTextContent("Port the publishers");
  });

  it("ANodeWithoutARecord_SaysNothingWasRecorded", async () => {
    world.detail = detail([attempt({ cause: null })]);
    await open();
    fireEvent.click(node("ticket"));
    expect(detailText()).toContain("Nothing recorded here in this attempt.");
  });

  it("DeriveNode_ShowsTheDeriveCostAndTheRefusedCutReviews", async () => {
    world.passes["r-1"] = passes("r-1", {}, 1.3);
    world.conversation = conversation([
      entry({ id: "c1", source: "spec-cut", kind: "cut-review", detail: { attempt: 1, deliverable: "refused", findings: [{ specId: "s2", criterion: "retry", problem: "both rewrite RetryPolicy.cs" }] } }),
      entry({ id: "c2", source: "spec-cut", kind: "cut-review", detail: { attempt: 2, deliverable: "accepted", findings: [] } }),
    ]);
    await open();
    expect(node("derive").textContent).toContain("1✗");
    fireEvent.click(node("derive"));
    expect(detailText()).toContain("$1.30");
    expect(detailText()).toContain("refused");
    expect(detailText()).toContain("both rewrite RetryPolicy.cs");
  });

  it("AnEarlierPassesVerify_ListsItsOwnCriterionVerdicts_NotTheSpecsFinalOnes", async () => {
    world.series["r-1"] = series("r-1", [spec()]);
    world.passes["r-1"] = passes("r-1", { s1: [
      pass({ ordinal: 1, endedBecause: "re-engaged", reengageReason: "red-with-open-steps", criteriaMet: 0, criteria: [{ criterion: "Builds", status: "unmet", evidence: "no test" }] }),
      pass({ ordinal: 2, passInSpec: 2, criteria: [{ criterion: "Builds", status: "met", evidence: "test added" }] }),
    ] });
    await open();
    fireEvent.click(node("v-s1-1"));
    expect(detailText()).toContain("Verify · spec 1 · pass 1");
    expect(detailText()).toContain("no test");
    expect(detailText()).not.toContain("test added");
  });

  it("AnOlderHandedBackAttempt_DrawsHandBackWithTheTextOfItsOwnConversationEntry", async () => {
    world.detail = detail([
      attempt({ runId: "r-1", handedBack: true, revision: null }),
      attempt({ number: 2, runId: "r-2", cause: { channel: "ticket_comment", actor: "h.operator", at: "2026-09-04T15:00:00Z" } }),
    ]);
    world.series["r-1"] = series("r-1", [], { handback: { case: "RequirementsContradictRepository", runId: "r-1", attempt: 1, at: "x" }, gate: { status: "skipped", specCount: 0, runId: "r-1" } });
    world.series["r-2"] = series("r-2", [spec()]);
    world.conversation = conversation([
      entry({ id: "h1", source: "spec-cut", kind: "hand-back", attemptRunId: "r-1", text: "Map the old headers or change the consumers?" }),
      entry({ id: "h2", source: "spec-cut", kind: "hand-back", attemptRunId: "r-2", text: "a later one" }),
    ]);
    await open();
    fireEvent.click(screen.getByTestId("attempt-switch-1"));
    await settle();
    await settle();
    fireEvent.click(node("handback"));
    expect(screen.getByTestId("handback-text")).toHaveTextContent("Map the old headers or change the consumers?");
    expect(detailText()).toContain("contradiction");
    expect(detailText()).toContain("Answered by h.operator");
    expect(detailText()).not.toContain("a later one");
  });

  it("ATerminalAttemptWithNoRevisionAndNoCut_LinksToItsRunViewer", async () => {
    world.detail = detail([attempt({ revision: null, status: "failed" })]);
    delete world.series["r-1"];
    await open();
    const fallback = screen.getByTestId("attempt-flow-fallback");
    expect(within(fallback).getByRole("link")).toHaveAttribute("href", "/jobs/r-1");
    expect(screen.queryByTestId("attempt-flow")).toBeNull();
  });

  it("ALiveAttemptsHandBackAndRefusedCutReview_AppearWithoutAReload", async () => {
    const live = attempt({ runId: "r-1", status: "running", finishedAt: null, revision: null });
    world.detail = detail([live]);
    delete world.series["r-1"];
    const hub = await open();
    expect(node("derive").getAttribute("class")).toContain("live");

    world.conversation = conversation([entry({ id: "c1", source: "spec-cut", kind: "cut-review", detail: { attempt: 1, deliverable: "refused", findings: [] } })]);
    act(() => {
      stream.events = [{ runId: "r-1", type: EventType.SpecCutReviewed, timestamp: "t", attempt: 1, deliverable: "refused", findings: [] } as RunEvent];
      stream.listeners.forEach((l) => l());
    });
    await settle(2000);
    await settle();
    expect(node("derive").textContent).toContain("1✗");

    world.detail = detail([{ ...live, status: "success", handedBack: true, finishedAt: "2026-09-04T14:30:00Z" }]);
    world.series["r-1"] = series("r-1", [], { handback: { case: "Refused", runId: "r-1", attempt: 1, at: "x" }, gate: { status: "skipped", specCount: 0, runId: "r-1" } });
    world.conversation = conversation([entry({ id: "h1", source: "spec-cut", kind: "hand-back", text: "Cannot be built as asked." })]);
    act(() => hub.runsChanged.emit("r-1"));
    await settle(2000);
    await settle(2000);
    await settle();
    fireEvent.click(node("handback"));
    expect(screen.getByTestId("handback-text")).toHaveTextContent("Cannot be built as asked.");
  });

  it("ANewAttemptAppearsAfterThisTicketsTicketActivityChanged", async () => {
    const hub = await open();
    expect(screen.queryByTestId("attempt-switch-2")).toBeNull();
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", status: "queued", finishedAt: null })]);
    act(() => hub.runsChanged.emit("r-2"));
    await settle(3000);
    expect(screen.queryByTestId("attempt-switch-2")).toBeNull();
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await settle(2000);
    await settle();
    expect(screen.getByTestId("attempt-switch-2")).toBeInTheDocument();
  });

  // 2026-10-13-8762w: the series strip sits between the header and the tabs, only for a real, budgeted series.
  const defaultSeries = (over: Parameters<typeof budget>[0] | null) =>
    series("r-1", [spec()], { budget: over === null ? null : budget(over) });

  it("TicketPage_NoBudget_RendersNoStrip", async () => {
    world.series.default = defaultSeries(null);
    await open();
    expect(screen.getByTestId("ticket-head")).toBeInTheDocument();
    expect(screen.queryByTestId("series-runway")).toBeNull();
  });

  it("TicketPage_BudgetOff_RendersNoStrip", async () => {
    world.series.default = defaultSeries({ next: "hold", heldBecause: "off" });
    await open();
    expect(screen.queryByTestId("series-runway")).toBeNull();
  });

  it("TicketPage_ActivityNudge_RefreshesTheStrip", async () => {
    world.series.default = defaultSeries({ next: "start" });
    const hub = await open();
    const strip = screen.getByTestId("series-runway");
    expect(strip.previousElementSibling).toBe(screen.getByTestId("ticket-head"));
    expect(strip.nextElementSibling).toHaveAttribute("role", "tablist");
    expect(screen.getByTestId("series-next")).toHaveTextContent("starts by itself");

    // The run ended (RunsChanged), and only afterwards the worker marks the ticket closed (TicketActivityChanged).
    act(() => hub.runsChanged.emit("r-1"));
    await settle(2000);
    await settle();
    expect(screen.getByTestId("series-next")).toHaveTextContent("starts by itself");
    world.series.default = defaultSeries({ next: "hold", heldBecause: "closed" });
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await settle(2000);
    await settle();
    expect(screen.getByTestId("series-next")).toHaveTextContent("held — a person closed the ticket");
  });

  it("TicketPage_HeldOnDefault_SelectsTheDefaultsTab", async () => {
    world.series.default = defaultSeries({ next: "hold", heldBecause: "default-pending" });
    await open();
    fireEvent.click(screen.getByTestId("series-go-defaults"));
    expect(nav.replace).toHaveBeenCalledWith(expect.stringContaining("tab=defaults"), { scroll: false });
  });
});
