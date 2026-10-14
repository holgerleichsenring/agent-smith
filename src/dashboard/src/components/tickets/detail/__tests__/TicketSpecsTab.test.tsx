import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, detail, pass, passes, reviewRound, reviews, series, spec } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketDetail, TicketReviewer, TicketReviews, TicketSeries } from "@/types/tickets";

// 2026-10-13-9a59r: the Specs and Acceptance tabs — the series as read-only documents with a cell per
// attempt from that attempt's own cut, steps as worked, revisions, acceptance per spec — and the cross-link
// from a spec node in the graph.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const nav = vi.hoisted(() => ({ search: "tab=specs", replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(nav.search),
  useRouter: () => ({ replace: nav.replace, push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const s1 = spec({
  specId: "s1", ordinal: 1, goal: "Port the publishers", state: "carried", executedBy: "r-1", executedByAttempt: 1,
  steps: [{ id: "1", action: "Swap the bus", target: null }, { id: "2", action: "Port publishers", target: null }],
  worked: [
    { stepId: "1", state: "done", doneByRunId: "r-1", doneByAttempt: 1, doneInPass: 2 },
    { stepId: "2", state: "done", doneByRunId: "r-1", doneByAttempt: 1, doneInPass: 1 },
  ],
  done: [
    { text: "Builds", verdict: "met", note: null, citation: null, overrule: null },
    { text: "Contracts unchanged", verdict: "met", note: null, citation: null, overrule: null },
  ],
});
const s2 = spec({
  specId: "s2", ordinal: 2, goal: "Replace the consumers", repos: ["portal-worker"], state: "carried", executedBy: "r-2", executedByAttempt: 2,
  done: [
    { text: "Same message types", verdict: "met", note: null, citation: null, overrule: { humanStatus: "unmet", reason: "one type dropped", author: "h.operator", at: "2026-09-04T15:00:00Z" } },
    { text: "Headers keep their names", verdict: "unmet", note: null, citation: null, overrule: null },
  ],
});
const s3 = spec({
  specId: "s3", ordinal: 3, goal: "Retry with backoff", repos: ["portal-worker"], state: "in_progress", executedBy: "r-3", executedByAttempt: 3,
  done: [{ text: "Backs off", verdict: null, note: null, citation: null, overrule: null }],
});

const world = {
  detail: null as unknown as TicketDetail,
  series: {} as Record<string, TicketSeries>,
  passes: {} as Record<string, RunPasses>,
  run: () => Promise.reject(new Error("unused")),
  compute: () => Promise.reject(new Error("unused")),
  reviews: reviews() as TicketReviews,
};
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn((_p: string, _t: string, runId: string | null) => Promise.resolve(world.series[runId ?? "default"])),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  passes: vi.fn((runId: string) => Promise.resolve(world.passes[runId] ?? passes(runId, {}, 0))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(world.reviews)),
};

const settle = async () => {
  for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(0));
};

async function open() {
  const { client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
}

const revision = (number: number) => ({ number, cause: `cause ${number}`, seriesId: "x", carriedIds: [], approvalAt: null });

beforeEach(() => {
  vi.useFakeTimers();
  nav.search = "tab=specs";
  nav.replace.mockClear();
  world.detail = detail([
    attempt({ revision: revision(1) }),
    attempt({ number: 2, runId: "r-2", status: "failed", revision: revision(2) }),
    attempt({ number: 3, runId: "r-3", status: "running", finishedAt: null, revision: revision(4) }),
  ]);
  world.series = {
    default: series("r-3", [s1, s2, s3], { attempt: 3 }),
    "r-1": series("r-1", [{ ...s1, state: "done" }]),
    "r-2": series("r-2", [s1, { ...s2, state: "done" }], { attempt: 2 }),
    "r-3": series("r-3", [s1, s2, s3], { attempt: 3 }),
  };
  world.passes = { "r-1": passes("r-1", { s1: [pass(), pass({ ordinal: 2, passInSpec: 2 })] }) };
  world.reviews = reviews();
});
afterEach(() => vi.useRealTimers());

describe("Ticket specs and acceptance tabs", () => {
  it("TheSpecsCounter_ReadsNAndTheNewestRevision", async () => {
    await open();
    expect(screen.getByTestId("ticket-tab-specs")).toHaveTextContent("Specs3 · rev 4");
    cleanup();
    world.detail = detail([attempt({ revision: null })]);
    await open();
    expect(screen.getByTestId("ticket-tab-specs").querySelector(".tc")).toHaveTextContent(/^3$/);
  });

  it("ACarriedSpec_ShowsACarriedCellForEachLaterAttempt_AndTheDocumentMarksEachStepWithItsAttemptAndPass", async () => {
    nav.search = "tab=specs&spec=s1";
    await open();
    const cell = (n: number) => screen.getByTestId(`spec-cell-s1-${n}`);
    expect(cell(1)).toHaveAttribute("data-kind", "done");
    expect(cell(1)).toHaveTextContent("2");
    expect(cell(2)).toHaveAttribute("data-kind", "carried");
    expect(cell(3)).toHaveAttribute("data-kind", "carried");
    expect(screen.getByTestId("spec-cell-s3-3")).toHaveAttribute("data-kind", "live");
    expect(screen.getByTestId("spec-cell-s3-1")).toHaveAttribute("data-kind", "none");
    const doc = screen.getByTestId("spec-doc");
    expect(within(doc).getByTestId("spec-doc-goal")).toHaveTextContent("Port the publishers");
    expect(within(doc).getByTestId("spec-step-1")).toHaveTextContent("A1 · pass 2");
    expect(within(doc).getByTestId("spec-step-2")).toHaveTextContent("A1 · pass 1");
    expect(screen.getByTestId("specs-revisions")).toHaveTextContent("rev 4");
  });

  it("WithoutASelection_TheLiveSpecIsShownFirst", async () => {
    await open();
    expect(screen.getByTestId("spec-doc-goal")).toHaveTextContent("Retry with backoff");
    expect(screen.getByTestId("spec-item-s3")).toHaveAttribute("aria-current", "true");
  });

  it("Acceptance_CountsVerifiedCriteriaAcrossSpecs_AndShowsAnOverruledCriterionAsOverruled", async () => {
    nav.search = "tab=acceptance";
    await open();
    expect(screen.getByTestId("acceptance-tally")).toHaveTextContent("3 of 5 verified");
    expect(screen.getByTestId("ticket-tab-acceptance")).toHaveTextContent("3/5");
    expect(screen.getByTestId("acceptance-spec-s2")).toHaveTextContent("Spec 2 · portal-worker");
    expect(within(screen.getByTestId("acceptance-spec-s2")).getByTestId("acceptance-overruled"))
      .toHaveTextContent("overruled: unmet by h.operator — one type dropped");
    expect(screen.getByTestId("acceptance-spec-s3")).toHaveTextContent("Spec 3 · portal-worker · working");
    expect(screen.getByTestId("acceptance-spec-s3")).toHaveTextContent("verify pending");
    expect(screen.getByTestId("acceptance-spec-s1")).toHaveTextContent("A1");
  });

  it("OpenTheSpec_OnAGraphNode_SelectsThatSpecOnTheSpecsTab", async () => {
    nav.search = "";
    await open();
    fireEvent.click(document.querySelector('[data-node="s2"]') as Element);
    fireEvent.click(screen.getByTestId("node-open-spec"));
    expect(nav.replace).toHaveBeenCalledWith("/tickets/shop/101?tab=specs&spec=s2", { scroll: false });
    cleanup();
    nav.search = "tab=specs&spec=s2";
    await open();
    expect(screen.getByTestId("spec-item-s2")).toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("spec-doc-goal")).toHaveTextContent("Replace the consumers");
  });

  it("TheCrossLinks_SetTheTab", async () => {
    nav.search = "";
    await open();
    fireEvent.click(screen.getByTestId("ticket-continue"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?tab=conversation", { scroll: false });
    fireEvent.click(screen.getByTestId("ticket-open-specs"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?tab=specs", { scroll: false });
  });
});

// 2026-10-13-8762l: the pre-build review rounds on the Specs tab — one spec's rounds per attempt and from the
// design conversation, and the set's series contract review with member buttons.

const cut = { specId: null, register: "goal", quote: "the old goal", replacement: "the new goal", evidence: "shop/src/A.cs:3 [A1]" };
const reviewer = (over: Partial<TicketReviewer> = {}): TicketReviewer => ({
  reviewer: 0, verdict: "build", cuts: [], findings: [], evidenceProblems: [], residues: [], ...over,
});

describe("Ticket specs tab review rounds", () => {
  it("ReviewRounds_CutsResiduesAndVerdict_Rendered", async () => {
    nav.search = "tab=specs&spec=s1";
    world.reviews = reviews({
      attempts: [{ runId: "r-1", number: 1, rounds: [reviewRound({
        verdict: "refuse", revisionNumber: 2, reviewers: [
          reviewer({ verdict: "build-with-cuts", cuts: [cut], residues: [{ register: "steps", quote: "old words" }],
            evidenceProblems: [{ claim: "A claim", problem: "no such file" }] }),
          reviewer({ reviewer: 1, verdict: "refuse", findings: [{ text: "The endpoint exists", evidence: "shop/src/B.cs:9 [A3]" }] }),
        ],
      })] }],
    });
    await open();
    const block = screen.getByTestId("review-rounds");
    expect(within(block).getByText("Attempt 1")).toBeInTheDocument();
    expect(within(block).getAllByTestId("review-verdict")[0]).toHaveTextContent("refuse");
    expect(within(block).getByTestId("review-cut")).toHaveTextContent("goal · “the old goal” → “the new goal” shop/src/A.cs:3 [A1]");
    expect(within(block).getByTestId("review-residue")).toHaveTextContent("steps · “old words” still stands");
    expect(within(block).getByTestId("review-evidence-problem")).toHaveTextContent("A claim — no such file");
    expect(within(block).getByTestId("review-finding")).toHaveTextContent("The endpoint exists shop/src/B.cs:9 [A3]");
    expect(within(block).getByTestId("review-round")).toHaveTextContent("rev 2");
    expect(within(block).getByTestId("review-round")).toHaveTextContent("reviewers: build-with-cuts, refuse");
  });

  it("ReviewRounds_ApprovedSet_ShowsConversationRounds", async () => {
    nav.search = "tab=specs&spec=s1";
    world.reviews = reviews({ conversation: { approvalAt: null, rounds: [reviewRound({
      scope: "conversation", verdict: "build-with-cuts", cuts: [cut], reviewers: [reviewer({ verdict: "build-with-cuts" }), reviewer({ reviewer: 1, verdict: null })],
    })] } });
    await open();
    const block = screen.getByTestId("review-rounds");
    expect(within(block).getByText("Reviewed in the conversation before approval")).toBeInTheDocument();
    expect(within(block).getByTestId("review-cut")).toHaveTextContent("“the new goal”");
    expect(within(block).getByTestId("review-round")).toHaveTextContent("reviewers: build-with-cuts, not taken");
  });

  it("ReviewRounds_RepoSearchEvidence_RenderedAsStored", async () => {
    nav.search = "tab=specs&spec=s1";
    world.reviews = reviews({ attempts: [{ runId: "r-1", number: 1, rounds: [reviewRound({
      reviewers: [reviewer({ cuts: [{ ...cut, evidence: "shop search:'Widget' [A2]" }] })],
    })] }] });
    await open();
    expect(screen.getByTestId("review-cut")).toHaveTextContent("shop search:'Widget' [A2]");
  });

  it("ReviewRounds_NoRoundAnywhere_SaysNoneRecorded", async () => {
    nav.search = "tab=specs&spec=s2";
    world.reviews = reviews({ attempts: [{ runId: "r-1", number: 1, rounds: [reviewRound({ specId: "s1" })] }] });
    await open();
    expect(screen.getByTestId("review-rounds-none")).toHaveTextContent("no review round was recorded");
    expect(screen.queryByTestId("series-review")).toBeNull();
  });

  it("TicketSpecsTab_SeriesRound_MemberButtonSelectsSpec", async () => {
    nav.search = "tab=specs&spec=s1";
    world.reviews = reviews({ attempts: [{ runId: "r-2", number: 2, rounds: [reviewRound({
      scope: "series", specId: null, verdict: "build-with-cuts",
      reviewers: [reviewer({ verdict: "build-with-cuts", cuts: [{ ...cut, specId: "s2" }], findings: [{ text: "Two owners of one event", evidence: "shop/src/E.cs:1 [A4]" }] })],
    })] }] });
    await open();
    const section = screen.getByTestId("series-review");
    expect(within(section).getByTestId("review-finding")).toHaveTextContent("Two owners of one event");
    fireEvent.click(within(section).getByTestId("series-member-s2"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?tab=specs&spec=s2", { scroll: false });
  });

  it("TicketSpecsTab_ApprovedSetSeriesRound_IsShown", async () => {
    world.reviews = reviews({ conversation: { approvalAt: null, rounds: [reviewRound({
      scope: "conversation", specId: "series", verdict: "refuse", findings: [{ text: "The contract misses a budget", evidence: "shop/src/A.cs:1 [A5]" }],
    })] } });
    await open();
    const section = screen.getByTestId("series-review");
    expect(within(section).getByText("Reviewed in the conversation before approval")).toBeInTheDocument();
    expect(within(section).getByTestId("review-verdict")).toHaveTextContent("refuse");
    expect(within(screen.getByTestId("review-rounds")).queryByText("The contract misses a budget")).toBeNull();
  });
});
