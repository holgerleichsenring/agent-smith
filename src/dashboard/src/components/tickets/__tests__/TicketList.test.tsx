import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketListView } from "@/types/tickets";
import { counts, fakeHub, list, pr, ticket } from "./ticketFixtures";

// 2026-10-13-9a59p: the ticket list against the server's JSON — sections and rail from one read,
// bucket views paging their own, the row anatomy, and answering from the list.

const hoisted = vi.hoisted(() => ({
  pathname: "/tickets",
  fetch: vi.fn(),
  apiFetch: vi.fn(),
  fetchRun: vi.fn(),
  push: vi.fn(),
}));

// 2026-10-15-4a4de: whether this sign-in reaches a project is useProjectReach's question; here it is an answer.
const reach = vi.hoisted(() => ({ value: "unknown" as "none" | "unknown" }));
vi.mock("@/hooks/useProjectReach", () => ({ useProjectReach: () => reach.value }));
vi.mock("next/navigation", () => ({
  usePathname: () => hoisted.pathname,
  useRouter: () => ({ push: hoisted.push }),
}));
vi.mock("@/lib/ticketsApi", () => ({ fetchTickets: (...args: unknown[]) => hoisted.fetch(...args) }));
vi.mock("@/lib/runsApi", () => ({ fetchRun: (...args: unknown[]) => hoisted.fetchRun(...args) }));
vi.mock("@/lib/apiResponse", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/apiResponse")>()),
  apiFetch: (...args: unknown[]) => hoisted.apiFetch(...args),
}));

import { ApiRefusal } from "@/lib/apiResponse";
import TicketsPage from "@/app/tickets/page";
import { TicketBucketFilterProvider } from "@/lib/TicketBucketFilter";
import { TicketListProvider } from "@/lib/tickets/TicketListProvider";
import { WorkRailSection } from "@/components/shell/WorkRailSection";

function serve(all: TicketListView, buckets: Record<string, TicketListView[]> = {}) {
  hoisted.fetch.mockImplementation((query: { bucket?: string; cursor?: string }) => {
    if (!query.bucket) return Promise.resolve(all);
    const pages = buckets[query.bucket] ?? [list([])];
    return Promise.resolve(query.cursor ? pages[1] : pages[0]);
  });
}

function renderTickets(search = "") {
  window.history.replaceState(null, "", `/tickets${search}`);
  return render(
    <TicketBucketFilterProvider>
      <TicketListProvider client={fakeHub().client}>
        <nav data-testid="rail"><WorkRailSection /></nav>
        <TicketsPage />
      </TicketListProvider>
    </TicketBucketFilterProvider>,
  );
}

const ago = (minutes: number) => new Date(Date.now() - minutes * 60000).toISOString();

beforeEach(() => {
  reach.value = "unknown";
  hoisted.pathname = "/tickets";
  hoisted.fetch.mockReset();
  hoisted.apiFetch.mockReset().mockResolvedValue({ ok: true, status: 202 });
  hoisted.fetchRun.mockReset().mockResolvedValue(null);
});

describe("TicketList", () => {
  // 2026-10-14-dbcen: filed tickets with no run on record.
  const neverRun = (id: string, bucket: "blocked" | "filed", waits: Array<[string, string]> = []) => ticket({
    ticketId: id, ticketKey: `github-${id}`, bucket, status: "", newestRunId: "", attemptCount: 0, cost: 0, attempts: [],
    since: ago(12), waitsFor: waits.map(([ticketId, key]) => ({ ticketId, project: "shop", key, title: key })), ticketText: true,
  });

  it("TicketList_BlockedAndFiledSections_RenderWithTheirCountsDotsAndWaitsForByKey", async () => {
    serve(list([neverRun("59", "blocked", [["58", "DPG-12"], ["41", "#41"]]), neverRun("61", "filed")], { blocked: 1, filed: 1 }));
    renderTickets();

    const blocked = await screen.findByTestId("ticket-section-blocked");
    expect(blocked).toHaveTextContent("Waits for another ticket");
    expect(screen.getByTestId("ticket-section-blocked-count")).toHaveTextContent("1");
    expect(within(blocked).getByTestId("ticket-dot").className).toBe("sd blocked");
    expect(within(blocked).getByTestId("ticket-norun")).toHaveTextContent("waits for DPG-12, #41");
    const filed = screen.getByTestId("ticket-section-filed");
    expect(filed).toHaveTextContent("Filed, no run on record");
    expect(within(filed).getByTestId("ticket-dot").className).toBe("sd filed");
    expect(within(screen.getByTestId("rail")).getByTestId("app-rail-count-Tickets")).toHaveTextContent("2");
  });

  it("TicketRow_NeverRunRow_ShowsWaitsForAndNoStripMarksAttemptsOrCost", async () => {
    serve(list([neverRun("59", "blocked", [["58", "#58"]]), neverRun("61", "filed")], { blocked: 1, filed: 1 }));
    renderTickets();

    for (const id of ["59", "61"]) {
      const row = await screen.findByTestId(`ticket-row-shop-${id}`);
      for (const cls of [".strip", ".attempts", ".cost"]) expect(row.querySelector(cls), `${id} ${cls}`).toBeNull();
      expect(row).not.toHaveTextContent(/attempts?|\$0\.00|0 specs/);
      expect(row).toHaveTextContent("filed 12m ago");
    }
    expect(screen.getByTestId("ticket-row-shop-59")).toHaveTextContent("waits for #58");
    expect(screen.getByTestId("ticket-row-shop-61")).toHaveTextContent("no run on record");
  });

  it("RailAndSections_CountsComeFromOneResponse", async () => {
    serve(list([ticket()], { needsYou: 0, working: 3, running: 2, queued: 1, waitingOnReview: 2, stuck: 1, idle: 4, done: 14 }));
    renderTickets();
    await screen.findByTestId("ticket-list");

    expect(hoisted.fetch).toHaveBeenCalledTimes(1);
    const rail = screen.getByTestId("rail");
    expect(within(rail).getByTestId("app-rail-count-Tickets")).toHaveTextContent("24");
    for (const [label, section, n] of [["Working", "working", "3"], ["Waiting on review", "waiting-on-review", "2"], ["Stuck", "stuck", "1"], ["Ended", "ended", "4"], ["Done", "done", "14"]]) {
      expect(within(rail).getByTestId(`app-rail-count-${label}`)).toHaveTextContent(n);
      expect(screen.getByTestId(`ticket-section-${section}-count`)).toHaveTextContent(n);
    }
    expect(within(rail).getByTestId("app-rail-item-Needs you")).toHaveAttribute("data-hot", "false");
    expect(screen.getByTestId("ticket-metric-working")).toHaveTextContent("2 running · 1 queued");
  });

  it("Row_OpensTheTicketPage", async () => {
    serve(list([ticket({ newestRunId: "r-9" })], { working: 1 }));
    renderTickets();
    expect(await screen.findByTestId("ticket-row-shop-101")).toHaveAttribute("href", "/tickets/shop/101");
  });

  it("StuckBucket_FetchesItsBucket_ShowsOnlyStuck_MarksItsRailEntry_AndPages", async () => {
    const stuck = (id: string) => ticket({ ticketId: id, bucket: "stuck", status: "failed", unchangedSinceLastAttempt: true });
    serve(list([ticket()], { working: 1, stuck: 2 }), {
      stuck: [list([stuck("1")], { working: 1, stuck: 2 }, "r-1"), list([stuck("2")], { working: 1, stuck: 2 })],
    });
    renderTickets("?bucket=stuck");

    await screen.findByTestId("ticket-row-shop-1");
    expect(hoisted.fetch).toHaveBeenCalledWith({ bucket: "stuck" }, expect.anything());
    expect([...document.querySelectorAll("section[data-testid]")].map((s) => (s as HTMLElement).dataset.testid)).toEqual(["ticket-section-stuck"]);
    expect(within(screen.getByTestId("rail")).getByTestId("app-rail-item-Stuck")).toHaveAttribute("data-active", "true");

    fireEvent.click(screen.getByTestId("tickets-load-more"));
    expect(await screen.findByTestId("ticket-row-shop-2")).toBeInTheDocument();
    expect(hoisted.fetch).toHaveBeenCalledWith({ bucket: "stuck", cursor: "r-1" });
    expect(screen.queryByTestId("tickets-load-more")).toBeNull();
  });

  it("ASectionWithMoreTicketsThanLoaded_EndsWithShowAll", async () => {
    serve(list([ticket()], { working: 3 }));
    renderTickets();
    const link = await screen.findByTestId("ticket-section-working-show-all");
    expect(link).toHaveTextContent("Show all 3");
    expect(link).toHaveAttribute("href", "/tickets?bucket=working");
  });

  it("CancelledAndPullRequestlessTickets_AreListedUnderEnded_WithTheirLastOutcome", async () => {
    const ended = (id: string, outcome: string) =>
      ticket({ ticketId: id, bucket: "idle", status: outcome, lastOutcome: outcome, attempts: [{ runId: `r-${id}`, status: outcome, endedOnInfrastructure: false, handedBack: false }] });
    serve(list([ended("1", "cancelled"), ended("2", "success")], { idle: 2 }));
    renderTickets();

    const section = await screen.findByTestId("ticket-section-ended");
    expect(within(section).getByTestId("ticket-row-shop-1")).toHaveTextContent("last attempt cancelled");
    expect(within(section).getByTestId("ticket-row-shop-2")).toHaveTextContent("last attempt succeeded");
    expect(section).toHaveTextContent("cancelled, or finished without a pull request");
  });

  it("ASucceededTicketWithoutAPullRequest_IsNotDrawnInTheStoppedColour", async () => {
    serve(list([ticket({ bucket: "idle", status: "success", lastOutcome: "success" })], { idle: 1 }));
    renderTickets();
    const dot = within(await screen.findByTestId("ticket-row-shop-101")).getByTestId("ticket-dot");
    expect(dot.className).toContain("ok");
    expect(dot.className).not.toContain("bad");
  });

  it("AQueuedWorkingTicket_DrawsTheQueuedDot", async () => {
    serve(list([ticket({ bucket: "working", status: "queued" })], { working: 1, queued: 1 }));
    renderTickets();
    const dot = within(await screen.findByTestId("ticket-row-shop-101")).getByTestId("ticket-dot");
    expect(dot.className).toBe("sd queued");
  });

  it("AttemptMarks_DrawEveryEndFromAttempts", async () => {
    const mark = (runId: string, status: string, endedOnInfrastructure = false, handedBack = false) => ({ runId, status, endedOnInfrastructure, handedBack });
    serve(list([ticket({ attemptCount: 5, attempts: [mark("a", "waiting_for_input", false, true), mark("b", "failed"), mark("c", "cancelled", true), mark("d", "success"), mark("e", "running")] })], { working: 1 }));
    renderTickets();
    const marks = within(await screen.findByTestId("ticket-attempts")).getAllByTitle(/·/).map((i) => i.dataset.mark);
    expect(marks).toEqual(["handed-back", "failed", "stopped", "ok", "running"]);
  });

  it("AParkedTicket_AnswersThroughPendingQuestionCard_LabelledWithItsAttempt", async () => {
    const question = { questionId: "q1", type: "Freeform", text: "Which header style?", context: null, choices: ["IETF draft"], defaultAnswer: null, askedAt: ago(41), answerDeadlineAt: ago(-60) };
    serve(list([ticket({ bucket: "needs_you", kind: "question", status: "waiting_for_input", attemptCount: 2, newestRunId: "r-q", pendingQuestion: { runId: "r-q", question }, seriesStrip: { derive: "cut", cells: [{ specId: "s1", state: "done" }, { specId: "s2", state: "asked" }] } })], { needsYou: 1 }));
    renderTickets();

    const send = await screen.findByRole("button", { name: "Send & resume attempt 2" });
    fireEvent.change(screen.getByTestId("pending-question-input"), { target: { value: "IETF draft" } });
    fireEvent.click(send);
    await waitFor(() => expect(hoisted.apiFetch).toHaveBeenCalledWith("/api/runs/r-q/answer", expect.objectContaining({ method: "POST" })));
    expect(screen.getByTestId("ticket-section-needs-you")).toHaveTextContent("Asked in spec 2");
    expect(hoisted.fetchRun).not.toHaveBeenCalled();
  });

  it("TicketNeedsYou_Handback_RendersMarkdown", async () => {
    serve(list([ticket({ bucket: "needs_you", kind: "handback", status: "waiting_for_input",
      handback: { text: "## Handed back\n\n<a href=\"#\" data-vss-mention=\"version:2.0,abc\">@Ada Example</a> decide.", at: ago(5) } })], { needsYou: 1 }));
    renderTickets();

    const card = await screen.findByTestId("ticket-handback");
    expect(card).toHaveTextContent("@Ada Example decide.");
    expect(card.textContent).not.toContain("##");
    expect(card.innerHTML).not.toContain("data-vss-mention");
  });

  it("AHandedBackTicket_ShowsTheRowsWords_WithoutAConversationFetch_AndLinksIntoTheConversation", async () => {
    const network = vi.spyOn(globalThis, "fetch");
    serve(list([ticket({ ticketId: "45/1", bucket: "needs_you", kind: "handback", status: "waiting_for_input", handback: { text: "No such table exists.", at: ago(180) } })], { needsYou: 1 }));
    renderTickets();

    expect((await screen.findByTestId("ticket-handback"))).toHaveTextContent("No such table exists.");
    expect(screen.getByTestId("ticket-handback-reply")).toHaveAttribute("href", "/spec-dialog?ticket=45%2F1&project=shop");
    // 2026-10-13-9a59q: the Needs-you row itself opens the ticket's page.
    expect(screen.getByTestId("ticket-row-shop-45/1")).toHaveAttribute("href", "/tickets/shop/45%2F1");
    expect(hoisted.fetch.mock.calls.every(([query]) => !("conversation" in query))).toBe(true);
    expect(hoisted.fetchRun).not.toHaveBeenCalled();
    expect(network).not.toHaveBeenCalled();
    network.mockRestore();
  });

  it("TheStrip_DrawsEverySeriesCell_AndNoneWithoutOne", async () => {
    const states = ["done", "carried", "live", "asked", "failed", "handed_back", "not_started"];
    serve(list([
      ticket({ ticketId: "1", seriesStrip: { derive: "cut", cells: states.map((state, i) => ({ specId: `s${i}`, state })) } }),
      ticket({ ticketId: "2", seriesStrip: null }),
    ], { working: 2 }));
    renderTickets();

    const strip = within(await screen.findByTestId("ticket-row-shop-1")).getByTestId("ticket-strip");
    expect([...strip.querySelectorAll("[data-cell]")].map((i) => (i as HTMLElement).dataset.cell)).toEqual(states);
    expect([...strip.querySelectorAll("[data-cell]")].map((i) => i.className)).toEqual(["done", "carried", "live", "ask", "bad", "ask", ""]);
    expect(within(screen.getByTestId("ticket-row-shop-2")).queryByTestId("ticket-strip")).toBeNull();
  });

  it("ThePrCells_FollowTheRecordsDecisionAndState", async () => {
    const prs = [
      pr({ url: "u1", decision: "none" }),
      pr({ url: "u2", decision: "approved", by: "k.lead" }),
      pr({ url: "u3", decision: "changes-requested", by: "k.lead" }),
      pr({ url: "u4", decision: "changes-requested-untrusted", by: "ext.contrib" }),
      pr({ url: "u5", decision: "rejected" }),
      pr({ url: "u6", state: "merged" }),
      pr({ url: "u7", state: "closed" }),
      pr({ url: "u8", state: "unsupported" }),
      pr({ url: "u9", state: "unread" }),
    ];
    serve(list([ticket({ bucket: "waiting_on_review", status: "success", pullRequests: prs })], { waitingOnReview: 1 }));
    renderTickets();

    const row = await screen.findByTestId("ticket-row-shop-101");
    expect([...row.querySelectorAll("[data-pr-state]")].map((i) => i.className)).toEqual([
      "p pr", "p pr appr", "p pr chg", "p pr untrusted", "p pr chg", "p done", "p pr ended", "p", "p",
    ]);
    expect(row).toHaveTextContent("changes requested by ext.contrib · not a trusted reviewer");
    expect(row).toHaveTextContent("approved by k.lead");
  });

  it("Done_IsCollapsedToThisMonthsLine_UntilShowDone", async () => {
    serve(list([ticket({ ticketId: "301", bucket: "done", status: "success" })], {
      done: 40, doneThisMonth: { tickets: 14, attempts: 24 }, lastDone: { project: "shop", ticketId: "301", at: ago(120) },
    }));
    renderTickets();

    const line = await screen.findByTestId("ticket-done-line");
    expect(line).toHaveTextContent("14 merged this month, last 2h ago (301)");
    expect(screen.getByTestId("ticket-section-done-count")).toHaveTextContent("40");
    expect(screen.queryByTestId("ticket-row-shop-301")).toBeNull();
    expect(screen.getByTestId("ticket-metric-done")).toHaveTextContent("1.7 attempts each");

    fireEvent.click(screen.getByTestId("ticket-done-show"));
    expect(screen.getByTestId("ticket-row-shop-301")).toBeInTheDocument();
  });

  it("ACallerWithoutRunsRead_SeesTheRefusalSurface", async () => {
    hoisted.fetch.mockRejectedValue(new ApiRefusal("/api/tickets", 403, "permission", ["runs.read"]));
    renderTickets();
    expect(await screen.findByTestId("refusal-surface")).toHaveTextContent("runs.read");
  });

  it("EmptyAndNoProjectReached_SaysSo", async () => {
    reach.value = "none";
    serve({ tickets: [], nextCursor: null, counts: counts() });
    renderTickets();
    expect(await screen.findByTestId("no-project-reach")).toHaveTextContent("Your sign-in reaches no project on this installation");
    expect(screen.queryByText(/No ticket has been worked yet/)).toBeNull();
  });

  it("EmptyForAGlobalReader_KeepsTodaysText", async () => {
    serve({ tickets: [], nextCursor: null, counts: counts() });
    renderTickets();
    expect(await screen.findByTestId("tickets-empty")).toHaveTextContent(/No ticket has been worked yet/);
    expect(screen.queryByTestId("no-project-reach")).toBeNull();
  });

  it("NoTicketYet_SaysSo", async () => {
    serve({ tickets: [], nextCursor: null, counts: counts() });
    renderTickets();
    expect(await screen.findByTestId("tickets-empty")).toBeInTheDocument();
  });
});
