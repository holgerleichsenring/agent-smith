import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub, ticket } from "../../__tests__/ticketFixtures";
import { conversation, defaults, detail, reviews, series, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketDerivation, TicketDetail, TicketRequires } from "@/types/tickets";

// 2026-10-26-bdd0f: the ticket page's spec derivations — one line each, apart from the attempts, with what came of
// it — and the Derive specs act, which posts the chosen mode and shows a refusal as the server wrote it.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(""),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/59",
}));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => false }));
const deriveSpecs = vi.hoisted(() => vi.fn());
vi.mock("@/lib/ticketsApi", async (original) => ({ ...(await original<object>()), deriveSpecs }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const world = { detail: null as unknown as TicketDetail };
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn(() => Promise.resolve(series("", [], { runId: null, gate: null, recorded: false }))),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] } as TicketRequires)),
};

function derivation(over: Partial<TicketDerivation>): TicketDerivation {
  return {
    runId: "d-1", status: "success", startedAt: "2026-10-26T09:00:00Z", finishedAt: "2026-10-26T09:04:00Z", cost: 0.4,
    outcome: "derived", specCount: 2, pullRequests: [], summary: "2 specs derived and stored on the ticket branch.", ...over,
  };
}

function page(derivations: TicketDerivation[]): TicketDetail {
  return detail([], {
    summary: ticket({ ticketId: "59", ticketKey: "github-59", status: "", newestRunId: "", attemptCount: 0, cost: 0,
      attempts: [], ticketText: true, waitsFor: [], bucket: "filed" }),
    derivations,
  });
}

async function open() {
  render(<TicketPageStore project="shop" ticketId="59" client={fakeHub().client} api={api}><TicketPage /></TicketPageStore>);
  await act(() => vi.advanceTimersByTimeAsync(0));
  await act(() => vi.advanceTimersByTimeAsync(0));
}

beforeEach(() => {
  vi.useFakeTimers();
  for (const fn of Object.values(api)) fn.mockClear();
  deriveSpecs.mockReset();
});
afterEach(() => vi.useRealTimers());

describe("SpecDerivations", () => {
  it("SpecDerivations_RendersEachOutcomeAndDeriveAgain", async () => {
    world.detail = page([
      derivation({ runId: "d-3", outcome: "not-finished", status: "failed", summary: "The repository could not be read" }),
      derivation({ runId: "d-2", outcome: "asked", summary: "Spec derivation stopped: which file format?" }),
      derivation({ runId: "d-1", pullRequests: ["https://example.test/pr/7"] }),
    ]);
    await open();

    expect(screen.getByTestId("spec-derivations")).toHaveTextContent("Spec derivations3");
    expect(screen.getByTestId("spec-derivation-outcome-d-3")).toHaveTextContent("not finished: The repository could not be read");
    expect(screen.getByTestId("spec-derivation-outcome-d-2")).toHaveTextContent("Spec derivation stopped: which file format?");
    expect(screen.getByTestId("spec-derivation-outcome-d-1")).toHaveTextContent("2 specs derived and stored on the ticket branch.");
    expect(screen.getByTestId("spec-derivation-d-1").querySelector("a")).toHaveAttribute("href", "https://example.test/pr/7");
    expect(screen.getByTestId("spec-derivation-d-3")).toHaveTextContent("Derive again");
  });

  it("SpecDerivations_EachLineShowsItsCost_SoFarWhileRunning", async () => {
    world.detail = page([
      derivation({ runId: "d-2", outcome: "running", status: "running", finishedAt: null, cost: 0.12, summary: null }),
      derivation({ runId: "d-1", cost: 1.375 }),
    ]);
    await open();

    expect(screen.getByTestId("spec-derivation-cost-d-1")).toHaveTextContent(/^\$1\.38$/);
    expect(screen.getByTestId("spec-derivation-d-1")).toHaveTextContent("4 min · $1.38");
    expect(screen.getByTestId("spec-derivation-cost-d-2")).toHaveTextContent("$0.12 so far");
  });

  it("DeriveSpecsAct_PostsTheChosenModeAndShowsARefusal", async () => {
    world.detail = page([]);
    deriveSpecs.mockResolvedValueOnce({ kind: "refused", text: "Another run is working on this ticket." });
    await open();

    expect(screen.queryByTestId("spec-derivations")).toBeNull();
    fireEvent.change(screen.getByTestId("derive-specs-mode"), { target: { value: "preview" } });
    await act(async () => { fireEvent.click(screen.getByTestId("derive-specs-start")); });

    expect(deriveSpecs).toHaveBeenCalledWith("shop", "59", "preview");
    expect(screen.getByTestId("derive-specs-refused")).toHaveTextContent("Another run is working on this ticket.");
  });

  it("DeriveSpecsAct_Started_ReReadsTheDetail", async () => {
    world.detail = page([]);
    deriveSpecs.mockResolvedValueOnce({ kind: "started", runId: "d-9" });
    await open();
    const reads = api.detail.mock.calls.length;

    await act(async () => { fireEvent.click(screen.getByTestId("derive-specs-start")); });

    expect(deriveSpecs).toHaveBeenCalledWith("shop", "59", "branch");
    expect(screen.getByTestId("derive-specs-started")).toHaveTextContent("Spec derivation started.");
    expect(api.detail.mock.calls.length).toBeGreaterThan(reads);
  });
});
