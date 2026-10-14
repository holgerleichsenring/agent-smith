import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, detail, pass, passes, series, spec, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketDetail, TicketSeries } from "@/types/tickets";

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
};
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn((_p: string, _t: string, runId: string | null) => Promise.resolve(world.series[runId ?? "default"])),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn((runId: string) => Promise.resolve(world.passes[runId] ?? passes(runId, {}, 0))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
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
