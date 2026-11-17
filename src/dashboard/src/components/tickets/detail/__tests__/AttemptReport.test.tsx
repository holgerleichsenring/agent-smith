import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaultEntry, defaults, detail, reviews, series, spec, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { DefaultVerdictAnswer, TicketAttempt, TicketDefaults } from "@/types/tickets";

// 2026-11-16-6877a: an attempt's report in the thread — its words, its run line with time and cost, and the spec it
// delivered as a card: criteria with verdicts, and the defaults it took kept or changed in place.
// 2026-11-17-8d1cb: the report is one line of What happened (outcome, end kind, sentence, duration) that opens to it.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const post = vi.hoisted(() => vi.fn<(...args: unknown[]) => Promise<DefaultVerdictAnswer>>());
vi.mock("@/lib/ticketsApi", async (original) => ({
  ...(await original<typeof import("@/lib/ticketsApi")>()),
  postDefaultVerdicts: (...args: unknown[]) => post(...args),
}));
const nav = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: nav.replace, push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));
const identity = vi.hoisted(() => ({ lacks: [] as string[] }));
// 2026-11-17-8d1cb: a run's line opens its sectioned log over the run's step rows.
const runSteps = vi.hoisted(() => ({ rows: [] as unknown[] }));
vi.mock("@/hooks/useRunSteps", () => ({ useRunSteps: () => runSteps.rows }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: (p: string) => identity.lacks.includes(p) }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { AttemptReport, TicketTextMessage } = await import("../ThreadMessages");

const world = { defaults: defaults() as TicketDefaults, text: ticketText() };
const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt()]))),
  series: vi.fn(() => Promise.resolve(series("r-1", [spec({ done: [
    { text: "Builds", verdict: "met", note: "dotnet build exited 0", citation: null, overrule: null },
    { text: "Retries back off", verdict: "unmet", note: null, citation: "RetryPolicy.cs:18", overrule: null },
  ] })]))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(world.defaults)),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
  text: vi.fn(() => Promise.resolve(world.text)),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
};

const settle = (ms = 0) => act(() => vi.advanceTimersByTimeAsync(ms));

async function report(a: TicketAttempt = attempt({ summary: "Done on branch agent-smith/GH-101." }), initiallyOpen = true) {
  render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><AttemptReport attempt={a} initiallyOpen={initiallyOpen} /></TicketPageStore>);
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  identity.lacks = [];
  runSteps.rows = [];
  world.defaults = defaults();
  world.text = ticketText();
  post.mockReset();
  post.mockResolvedValue({ kind: "applied", applied: [1], alreadyDecided: [] });
  nav.replace.mockClear();
});
afterEach(() => vi.useRealTimers());

describe("AttemptReport", () => {
  it("AttemptReport_SaysTheRunsOwnWords_AndLinksTheRunWithTimeAndCost", async () => {
    await report();
    expect(screen.getByTestId("thread-report-1")).toHaveTextContent("Done on branch agent-smith/GH-101.");
    // The line already says a one-line end text; the body does not repeat it.
    expect(screen.queryByTestId("thread-report-text")).toBeNull();
    expect(screen.getByTestId("thread-run-1")).toHaveAttribute("href", "/jobs/r-1");
    expect(screen.getByTestId("thread-run-1")).toHaveTextContent("Run 1 · 30m 0s · $3.05 · open the run ›");
  });

  it("AttemptReport_Closed_IsOneLineWithOutcomeEndKindSentenceAndDuration_AndOpensOnAClick", async () => {
    await report(attempt({ status: "failed", endKind: "image-untrusted", summary: "Registry ghcr.io/x is not trusted.\nmore" }), false);
    expect(screen.queryByTestId("thread-report-text")).toBeNull();
    const line = screen.getByTestId("thread-report-1");
    expect(line).toHaveTextContent("Run 1failed · image not trusted · Registry ghcr.io/x is not trusted.30m 0s▸");
    expect(line).toHaveClass("bad");
    expect(screen.queryByTestId("thread-run-1")).toBeNull();
    fireEvent.click(screen.getByTestId("thread-report-1-head"));
    expect(screen.getByTestId("thread-run-1")).toBeInTheDocument();
    expect(screen.getByTestId("thread-report-text")).toHaveTextContent(/Registry ghcr.io\/x is not trusted.\s+more/);
  });

  it("AttemptReport_Opened_ShowsTheRunsSectionedLog_AStoppedSectionOpenByItself", async () => {
    runSteps.rows = [
      { stepIndex: 0, stepName: "CheckoutSource", displayName: "Check out", commandName: "CheckoutSourceCommand", status: "success", durationSeconds: 4, resultMessage: "cloned", llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0 },
      { stepIndex: 1, stepName: "VerifySpec", displayName: "Verify", commandName: "VerifySpecCommand", status: "failed", durationSeconds: 9, resultMessage: "dotnet test · 1 failed", llmCalls: 0, costUsd: 0, sandboxCommands: 1, subAgents: 0 },
    ];
    await report(attempt({ status: "failed" }));
    const log = screen.getByTestId("thread-runlog-1");
    expect(within(log).getByTestId("run-section-verify")).toHaveAttribute("data-state", "bad");
    expect(within(log).getAllByTestId("run-log-line").map((l) => l.textContent)).toEqual(["Verify: dotnet test · 1 failed"]);
  });

  it("AttemptReport_Live_SaysItIsWorking", async () => {
    await report(attempt({ status: "running", finishedAt: null }));
    expect(screen.getByTestId("thread-report-1")).toHaveTextContent("Run 1 is working.");
  });

  it("SpecCard_ClosedNamesTheTally_OpenListsVerdictsAndOpensTheWholeSpec", async () => {
    await report();
    const card = screen.getByTestId("spec-card-s1");
    expect(within(card).getByTestId("spec-card-tally")).toHaveTextContent("1 of 2 done criteria met");
    expect(within(card).queryByTestId("spec-card-criterion")).toBeNull();
    fireEvent.click(within(card).getByRole("button", { expanded: false }));
    const criteria = within(card).getAllByTestId("spec-card-criterion");
    expect(criteria.map((c) => c.getAttribute("data-verdict"))).toEqual(["met", "unmet"]);
    expect(criteria[0]).toHaveTextContent("dotnet build exited 0");
    expect(criteria[1]).toHaveTextContent("RetryPolicy.cs:18");
    fireEvent.click(within(card).getByTestId("spec-card-open"));
    expect(nav.replace).toHaveBeenCalledWith("/tickets/shop/101?section=specs&spec=s1", { scroll: false });
  });

  it("SpecCard_DecidedForYou_KeepRatifies_ChangeOverrules_AConversationDefaultOffersKeepOnly", async () => {
    world.defaults = defaults([defaultEntry({ id: 1 }), defaultEntry({ id: 2, conversation: "d-1", runId: "r-1" }),
      defaultEntry({ id: 3, verdict: "ratified", verdictBy: "m.reviewer" })]);
    await report();
    fireEvent.click(within(screen.getByTestId("spec-card-s1")).getByRole("button", { expanded: false }));
    expect(screen.getByTestId("spec-card-default-1")).toHaveTextContent("Decided for you: IETF draft");
    fireEvent.click(screen.getByTestId("spec-card-keep-1"));
    await settle();
    expect(post).toHaveBeenLastCalledWith("shop", "101", [{ id: 1, verdict: "ratified" }]);
    fireEvent.click(screen.getByTestId("spec-card-change-1"));
    await settle();
    expect(post).toHaveBeenLastCalledWith("shop", "101", [{ id: 1, verdict: "overruled" }]);
    expect(screen.queryByTestId("spec-card-change-2")).toBeNull();
    expect(screen.getByTestId("spec-card-keep-2")).toBeInTheDocument();
    expect(screen.getByTestId("spec-card-verdict-3")).toHaveTextContent("kept by m.reviewer");
  });

  it("SpecCard_WithoutRunsControl_OffersNoKeepOrChange", async () => {
    identity.lacks = ["runs.control"];
    world.defaults = defaults([defaultEntry({ id: 1 })]);
    await report();
    fireEvent.click(within(screen.getByTestId("spec-card-s1")).getByRole("button", { expanded: false }));
    expect(screen.queryByTestId("spec-card-keep-1")).toBeNull();
  });
});

describe("TicketTextMessage", () => {
  it("TicketTextMessage_IsOneLineWithTheFirstSentence_AndOpensTheTicketAsWritten", async () => {
    world.text = ticketText({ description: "<p>Export the report. Then mail it.</p>", descriptionFormat: "html", acceptanceCriteria: "Retries back off." });
    render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><TicketTextMessage /></TicketPageStore>);
    await settle();
    expect(screen.getByTestId("thread-ticket")).toHaveTextContent("Export the report.");
    expect(screen.queryByText("Retries back off.")).toBeNull();
    fireEvent.click(screen.getByTestId("thread-ticket-head"));
    expect(screen.getByTestId("ticket-text")).toBeInTheDocument();
    expect(screen.getByText("Retries back off.")).toBeInTheDocument();
  });
});
