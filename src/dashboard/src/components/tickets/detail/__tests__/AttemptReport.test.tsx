import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaultEntry, defaults, detail, reviews, series, spec, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { DefaultVerdictAnswer, TicketAttempt, TicketDefaults } from "@/types/tickets";

// 2026-11-16-6877a: an attempt's report in the thread — its words, its run line with time and cost, and the spec it
// delivered as a card: criteria with verdicts, and the defaults it took kept or changed in place.

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

async function report(a: TicketAttempt = attempt({ summary: "Done on branch agent-smith/GH-101." })) {
  render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><AttemptReport attempt={a} /></TicketPageStore>);
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  identity.lacks = [];
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
    expect(screen.getByTestId("thread-report-text")).toHaveTextContent("Done on branch agent-smith/GH-101.");
    expect(screen.getByTestId("thread-run-1")).toHaveAttribute("href", "/jobs/r-1");
    expect(screen.getByTestId("thread-run-1")).toHaveTextContent("Run 1 · 30m 0s · $3.05 · see what it did");
  });

  it("AttemptReport_Live_SaysItIsWorking", async () => {
    await report(attempt({ status: "running", finishedAt: null }));
    expect(screen.getByTestId("thread-report-text")).toHaveTextContent("Run 1 is working.");
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
    expect(nav.replace).toHaveBeenCalledWith("/tickets/shop/101?tab=specs&spec=s1", { scroll: false });
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
  it("TicketTextMessage_ComesShort_AndShowsTheTicketAsWrittenOnAClick", async () => {
    world.text = ticketText({ description: "A".repeat(400), acceptanceCriteria: "Retries back off." });
    render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><TicketTextMessage /></TicketPageStore>);
    await settle();
    expect(screen.getByTestId("thread-ticket-text")).toHaveClass("tk-short");
    expect(screen.queryByText("Retries back off.")).toBeNull();
    fireEvent.click(screen.getByTestId("thread-ticket-toggle"));
    expect(screen.getByTestId("thread-ticket-text")).not.toHaveClass("tk-short");
    expect(screen.getByText("Retries back off.")).toBeInTheDocument();
    expect(screen.getByTestId("thread-ticket-toggle")).toHaveTextContent("Show less");
  });
});
