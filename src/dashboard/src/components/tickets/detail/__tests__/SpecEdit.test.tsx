import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaults, detail, reviews, series, spec, ticketText } from "./detailFixtures";
import type { RunEvent } from "@/types/hub-events";
import type { TicketDetail, TicketSeries } from "@/types/tickets";

// 2026-11-17-dd05c: a spec that has not run is edited in place — its yaml, Save and the server's sentence; one that ran
// keeps the read-only note and has no Edit.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const nav = vi.hoisted(() => ({ search: "section=specs&spec=s2" }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(nav.search),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));
const edits = vi.hoisted(() => ({ editTicketSpec: vi.fn() }));
vi.mock("@/lib/ticketSpecEditApi", () => edits);

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const ran = spec({ specId: "s1", ordinal: 1, state: "done", executedBy: "r-1", executedByAttempt: 1 });
const open = spec({ specId: "s2", ordinal: 2, goal: "Retry with backoff", state: "pending", executedBy: null, executedByAttempt: null,
  yaml: "spec: s2\ngoal: Retry with backoff\n" });

const world = { detail: null as unknown as TicketDetail, series: null as unknown as TicketSeries };
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn(() => Promise.resolve(world.series)),
  conversation: vi.fn(() => Promise.resolve(conversation([]))),
  text: vi.fn(() => Promise.resolve(ticketText())),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
  requires: vi.fn(() => Promise.resolve({ requires: [], requiredBy: [] })),
};

const settle = async () => {
  for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(0));
};

async function show() {
  const { client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
}

beforeEach(() => {
  vi.useFakeTimers();
  nav.search = "section=specs&spec=s2";
  world.detail = detail([attempt({})]);
  world.series = series("r-1", [ran, open]);
  edits.editTicketSpec.mockReset();
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("SpecEdit", () => {
  it("SpecEdit_Save_SendsTheEditedYaml_AndShowsTheServersSentence", async () => {
    edits.editTicketSpec.mockResolvedValue({ recorded: true, text: "The approved series under ticket 101 is the one you just approved (2 spec(s))." });
    await show();

    fireEvent.click(screen.getByTestId("spec-edit"));
    const box = screen.getByTestId("spec-edit-yaml") as HTMLTextAreaElement;
    expect(box.value).toBe("spec: s2\ngoal: Retry with backoff\n");
    fireEvent.change(box, { target: { value: "spec: s2\ngoal: Retry with jitter\n" } });
    fireEvent.click(screen.getByTestId("spec-edit-save"));
    await settle();

    expect(edits.editTicketSpec).toHaveBeenCalledWith("shop", "101", "s2", "spec: s2\ngoal: Retry with jitter\n");
    expect(screen.queryByTestId("spec-edit-yaml")).toBeNull();
    expect(screen.getByTestId("spec-edit-said")).toHaveTextContent("the one you just approved");
  });

  it("SpecEdit_Refused_KeepsTheYamlAndSaysWhy", async () => {
    edits.editTicketSpec.mockResolvedValue({ recorded: false, text: "Nothing was recorded: the yaml is not a valid spec." });
    await show();

    fireEvent.click(screen.getByTestId("spec-edit"));
    fireEvent.click(screen.getByTestId("spec-edit-save"));
    await settle();

    expect(screen.getByTestId("spec-edit-yaml")).toBeInTheDocument();
    expect(screen.getByTestId("spec-edit-said")).toHaveTextContent("not a valid spec");
  });

  it("SpecEdit_ASpecThatRan_HasNoEdit", async () => {
    nav.search = "section=specs&spec=s1";
    await show();

    expect(screen.queryByTestId("spec-edit")).toBeNull();
    expect(screen.getByTestId("spec-discuss")).toBeInTheDocument();
  });
});
