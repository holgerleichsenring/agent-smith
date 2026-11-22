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
// 2026-11-18-d6d2e: the log is the run page's RunSectionedLog — its specs and an open step's events are read too.
vi.mock("@/hooks/useRunSpecs", () => ({ useRunSpecs: () => [] }));
vi.mock("@/hooks/useRunStepEvents", () => ({ useRunStepEvents: () => ({ events: [], hasOlder: false, loading: false, loadOlder: () => {} }) }));
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

function row(stepIndex: number, command: string, displayName: string, over: Record<string, unknown> = {}) {
  return { stepIndex, stepName: command, displayName, commandName: `${command}Command`, status: "success", durationSeconds: null,
    resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over };
}

const settle = (ms = 0) => act(() => vi.advanceTimersByTimeAsync(ms));

async function report(a: TicketAttempt = attempt({ summary: "Done on branch agent-smith/GH-101." }), initiallyOpen = true, log = true) {
  render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><AttemptReport attempt={a} initiallyOpen={initiallyOpen} log={log} /></TicketPageStore>);
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

  // 2026-11-18-d6d2e: the run page's log — a section per spec, a planned spec hollow, a stopped one open by itself.
  it("AttemptReport_Opened_ShowsTheRunPagesLog_ASectionPerSpec_APlannedSpecPending_NoTicketSection", async () => {
    runSteps.rows = [
      row(0, "CheckoutSource", "Check out", { status: "success" }),
      row(1, "AgenticMaster", "Run master skill", { specId: "s1", status: "success", llmCalls: 12, costUsd: 0.4 }),
      row(2, "VerifySpec", "Build and test", { specId: "s1", status: "failed" }),
      row(3, "AgenticMaster", "Run master skill", { specId: "s2", planned: true, status: null }),
      row(4, "VerifySpec", "Build and test", { specId: "s2", planned: true, status: null }),
    ];
    await report(attempt({ status: "failed" }));
    const log = screen.getByTestId("thread-runlog-1");
    expect(within(log).getByTestId("run-section-spec:s1")).toHaveAttribute("data-state", "bad");
    expect(within(log).getByTestId("run-section-meta-spec:s1")).toHaveTextContent("1 of 2 steps done · 12 llm · $0.40");
    expect(within(log).getByTestId("run-section-spec:s2")).toHaveAttribute("data-state", "pending");
    expect(within(log).getByTestId("run-section-meta-spec:s2")).toHaveTextContent("0 of 2 steps done");
    expect(within(log).queryByTestId("run-section-ticket")).toBeNull();
    // 2026-11-19-1b72c: a run that recorded no verdict and no report has no check section.
    expect(within(log).queryByTestId("run-section-check")).toBeNull();
    // The stopped spec opens by itself; the planned one stays closed.
    expect(within(log).getByTestId("run-step-2")).toHaveAttribute("id", "r-r-1-step-2");
    expect(within(log).queryByTestId("run-step-3")).toBeNull();
  });

  // 2026-11-22-4321a: the Conversation section draws a report without the run log; the body and the run link stay.
  it("AttemptReport_WithoutTheLog_OpensItsBodyWithNoRunLog", async () => {
    runSteps.rows = [row(0, "CheckoutSource", "Check out", { status: "success" })];
    await report(attempt({ status: "failed" }), true, false);
    expect(screen.queryByTestId("thread-runlog-1")).toBeNull();
    expect(screen.getByTestId("thread-run-1")).toHaveAttribute("href", "/jobs/r-1");
  });

  // 2026-11-18-98fa: the check of a run whose master ended with finish_spec is its report and its follow-ups.
  it("AttemptReport_CheckSection_ShowsTheMastersReportAsMarkdown_AndItsFollowUpsToFile", async () => {
    runSteps.rows = [row(1, "AgenticMaster", "Run master skill", { specId: "s1", status: "success" })];
    api.run.mockImplementationOnce(() => Promise.resolve({ runId: "r-1", status: "success",
      acceptance: { criteria: [], outcome: null, ratifiedBy: null, reports: [{ markdown: "## Delivered", specId: "s1" }] } } as never));
    api.series.mockImplementationOnce(() => Promise.resolve(series("r-1", [spec({
      done: [{ text: "Builds", verdict: null, note: null, citation: null, overrule: null }],
      report: "## Delivered\n\n- the **retry** policy",
      followUps: [{ goal: "Exports stream CSV", why: "paging first", runId: "r-1", index: 0 }],
    })])));
    await report();
    const log = screen.getByTestId("thread-runlog-1");
    fireEvent.click(within(within(log).getByTestId("run-section-check")).getByRole("button", { name: /The master's report/ }));
    const check = within(log).getByTestId("thread-runlog-check-1");
    expect(within(check).getByTestId("master-report-s1").querySelector("li")).toHaveTextContent("the retry policy");
    expect(check).not.toHaveTextContent("overrule");
    expect(within(check).getByTestId("acceptance-follow-up")).toHaveTextContent("Exports stream CSV");
    expect(within(check).getByTestId("follow-up-file")).toHaveTextContent("File as a ticket");
    expect(within(check).getByRole("link", { name: "Open the run" })).toHaveAttribute("href", "/jobs/r-1");
  });

  it("AttemptReport_OpeningAStep_ShowsItsDetail_AndLeavesTheAddressAlone", async () => {
    runSteps.rows = [row(1, "VerifySpec", "Build and test", { specId: "s1", status: "failed" })];
    await report(attempt({ status: "failed" }));
    const before = window.location.href;
    fireEvent.click(within(screen.getByTestId("run-step-1")).getByTestId("run-log-line"));
    expect(screen.getByTestId("run-step-detail-1")).toBeInTheDocument();
    expect(window.location.href).toBe(before);
  });

  it("AttemptReport_Live_SaysItIsWorking", async () => {
    await report(attempt({ status: "running", finishedAt: null }));
    expect(screen.getByTestId("thread-report-1")).toHaveTextContent("Run 1 is working.");
  });

  // 2026-11-19-1b72c: a spec that recorded no verdict and no report names no tally — no "no verdict" line.
  it("SpecCard_NoVerdict_NamesNoTally", async () => {
    api.series.mockImplementationOnce(() => Promise.resolve(series("r-1", [spec({ verdictRecorded: false })])));
    await report();
    const card = screen.getByTestId("spec-card-s1");
    expect(within(card).queryByTestId("spec-card-tally")).toBeNull();
    expect(card).not.toHaveTextContent(/no verdict|done criteria met/);
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
