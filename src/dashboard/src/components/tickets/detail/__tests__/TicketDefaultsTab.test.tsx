import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaultEntry, defaults, detail, reviews, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { DefaultVerdictAnswer, TicketDefaults } from "@/types/tickets";

// 2026-10-13-8762q: the Defaults tab as a person meets it — pending before decided, a held series marked, a
// memory proposal by its pointer, a design-conversation default ratify-only, and one act for a selection.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const post = vi.hoisted(() => vi.fn<(...args: unknown[]) => Promise<DefaultVerdictAnswer>>());
vi.mock("@/lib/ticketsApi", async (original) => ({
  ...(await original<typeof import("@/lib/ticketsApi")>()),
  postDefaultVerdicts: (...args: unknown[]) => post(...args),
}));
const identity = vi.hoisted(() => ({ lacks: [] as string[] }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: (p: string) => identity.lacks.includes(p) }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketDefaultsTab, ANSWER_IN_CONVERSATION } = await import("../TicketDefaultsTab");

const world = { defaults: defaults() as TicketDefaults };
const api = {
  detail: vi.fn(() => Promise.resolve(detail([attempt()]))),
  series: vi.fn(() => Promise.resolve(series("r-1", []))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(world.defaults)),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
};

const settle = (ms = 0) => act(() => vi.advanceTimersByTimeAsync(ms));

async function open(...entries: Parameters<typeof defaults>[0]) {
  world.defaults = defaults(entries);
  render(<TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={api}><TicketDefaultsTab /></TicketPageStore>);
  await settle();
}

const select = (id: number) => fireEvent.click(screen.getByTestId(`default-select-${id}`));

beforeEach(() => {
  vi.useFakeTimers();
  identity.lacks = [];
  post.mockReset();
  for (const fn of Object.values(api)) fn.mockClear();
});
afterEach(() => vi.useRealTimers());

describe("TicketDefaultsTab", () => {
  it("TicketDefaultsTab_PendingBeforeDecided_HeldSeriesMarked", async () => {
    await open(
      defaultEntry({ id: 3, holdsSeries: true, question: "Which cap?" }),
      defaultEntry({ id: 2 }),
      defaultEntry({ id: 1, verdict: "ratified", verdictBy: "m.reviewer", verdictAt: "2026-09-04T15:00:00Z", verdictNote: "fine" }),
    );
    const pending = screen.getByTestId("defaults-pending");
    expect(within(pending).getAllByTestId(/^default-\d+$/).map((r) => r.dataset.testid)).toEqual(["default-3", "default-2"]);
    expect(screen.getByTestId("default-3")).toHaveClass("held");
    expect(within(screen.getByTestId("default-3")).getByTestId("default-holds")).toHaveTextContent("holds the series");
    expect(within(screen.getByTestId("default-2")).queryByTestId("default-holds")).toBeNull();
    const decided = within(screen.getByTestId("defaults-decided")).getByTestId("default-1");
    expect(within(decided).getByTestId("default-verdict")).toHaveTextContent(/ratified.*by m\.reviewer.*“fine”/);
    expect(within(decided).queryByRole("checkbox")).toBeNull();
    const body = within(screen.getByTestId("default-2")).getByTestId("default-body");
    expect(body).toHaveTextContent("IETF draft");
    expect(body).toHaveTextContent("X-RateLimit-*");
    expect(body).toHaveTextContent("The draft is what clients read.");
    expect(screen.getByTestId("default-2")).toHaveTextContent("spec s1");
    expect(within(screen.getByTestId("default-2")).getByTestId("default-origin")).toHaveTextContent("attempt 1");
  });

  it("TicketDefaultsTab_MemoryProposal_ShowsPointerNotBody", async () => {
    await open(defaultEntry({
      id: 4, kind: "memory-proposal", question: "Ratify this proposed feedback memory?", chosen: null, alternatives: [], why: null,
      pointerPath: "shop-api/.agentsmith/memory/feedback-retry-caps.md",
    }));
    const row = screen.getByTestId("default-4");
    expect(within(row).getByTestId("default-pointer")).toHaveTextContent("shop-api/.agentsmith/memory/feedback-retry-caps.md · feedback-retry-caps");
    expect(within(row).queryByTestId("default-body")).toBeNull();
    expect(within(row).queryByTestId("default-not-landed")).toBeNull();
  });

  it("TicketDefaultsTab_MemoryProposalNotLanded_SaysNotLanded", async () => {
    await open(defaultEntry({ id: 4, kind: "memory-proposal", pointerPath: "a/.agentsmith/memory/x.md", landed: false }));
    expect(within(screen.getByTestId("default-4")).getByTestId("default-not-landed")).toHaveTextContent("not landed");
  });

  it("TicketDefaultsTab_ConversationDefault_SaysInTheDesignConversation", async () => {
    await open(defaultEntry({
      id: 5, attempt: null, conversation: "dlg-1", question: null, chosen: null, alternatives: null, why: null,
    }));
    const row = screen.getByTestId("default-5");
    expect(within(row).getByTestId("default-origin")).toHaveTextContent("in the design conversation");
    expect(within(row).queryByTestId("default-body")).toBeNull();
  });

  it("TicketDefaultsTab_NonAttemptRunRow_ShowsItsRunId", async () => {
    await open(defaultEntry({ id: 6, attempt: null, runId: "r-scan-9" }));
    expect(within(screen.getByTestId("default-6")).getByTestId("default-origin")).toHaveTextContent("run r-scan-9");
  });

  it("TicketDefaultsTab_ConversationRow_OffersRatifyOnly", async () => {
    await open(defaultEntry({ id: 5, attempt: null, conversation: "dlg-1" }), defaultEntry({ id: 2 }));
    select(2);
    expect(screen.getByTestId("defaults-overrule")).toBeEnabled();
    select(5);
    expect(screen.getByTestId("defaults-overrule")).toBeDisabled();
    expect(screen.getByTestId("defaults-conversation-hint")).toHaveTextContent(ANSWER_IN_CONVERSATION);
    expect(screen.getByTestId("defaults-ratify")).toBeEnabled();
  });

  it("TicketDefaultsTab_RatifySelected_PostsOneBatch_AndShowsApplied", async () => {
    await open(defaultEntry({ id: 1 }), defaultEntry({ id: 2 }), defaultEntry({ id: 3 }));
    post.mockResolvedValue({ kind: "applied", applied: [1, 2, 3], alreadyDecided: [] });
    fireEvent.click(screen.getByTestId("defaults-select-all"));
    fireEvent.click(screen.getByTestId("defaults-ratify"));
    await settle();
    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith("shop", "101", [
      { id: 1, verdict: "ratified" }, { id: 2, verdict: "ratified" }, { id: 3, verdict: "ratified" },
    ]);
    expect(screen.getByTestId("defaults-outcome")).toHaveTextContent("3 defaults applied.");
    await settle(2000);
    expect(api.defaults).toHaveBeenCalledTimes(2);
  });

  it("TicketDefaultsTab_OverruleSelected_SendsTheNoteWithEveryItem", async () => {
    await open(defaultEntry({ id: 1 }), defaultEntry({ id: 2 }), defaultEntry({ id: 3 }));
    post.mockResolvedValue({ kind: "applied", applied: [1, 3], alreadyDecided: [] });
    select(1);
    select(3);
    fireEvent.change(screen.getByTestId("defaults-note"), { target: { value: "  use the 30 s cap  " } });
    fireEvent.click(screen.getByTestId("defaults-overrule"));
    await settle();
    expect(post).toHaveBeenCalledWith("shop", "101", [
      { id: 1, verdict: "overruled", note: "use the 30 s cap" }, { id: 3, verdict: "overruled", note: "use the 30 s cap" },
    ]);
  });

  it("TicketDefaultsTab_AlreadyDecided_IsSaidWithItsStandingVerdict", async () => {
    await open(defaultEntry({ id: 1 }), defaultEntry({ id: 2, question: "Which retry header?" }));
    post.mockResolvedValue({ kind: "applied", applied: [1], alreadyDecided: [{ id: 2, verdict: "overruled" }] });
    fireEvent.click(screen.getByTestId("defaults-select-all"));
    fireEvent.click(screen.getByTestId("defaults-ratify"));
    await settle();
    expect(screen.getByTestId("defaults-outcome")).toHaveTextContent("1 default applied.");
    expect(screen.getByTestId("defaults-already-decided")).toHaveTextContent("“Which retry header?” was already overruled.");
  });

  it("TicketDefaultsTab_Refused400_ShowsTheServerText", async () => {
    await open(defaultEntry({ id: 1 }));
    post.mockResolvedValue({ kind: "refused", text: "item 1 (default #1): unknown\nNothing was written." });
    select(1);
    fireEvent.click(screen.getByTestId("defaults-ratify"));
    await settle();
    expect(screen.getByTestId("defaults-refused")).toHaveTextContent("item 1 (default #1): unknown Nothing was written.");
  });

  it("TicketDefaultsTab_CallerLackingRunsControl_SeesNoActions", async () => {
    identity.lacks = ["runs.control"];
    await open(defaultEntry({ id: 1 }), defaultEntry({ id: 2, holdsSeries: true }));
    expect(screen.getByTestId("default-1")).toBeInTheDocument();
    expect(screen.queryByTestId("defaults-bar")).toBeNull();
    expect(screen.queryAllByRole("checkbox")).toHaveLength(0);
  });
});
