import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub, pr } from "../../__tests__/ticketFixtures";
import { attempt, budget, conversation, defaults, detail, passes, reviews, series, spec, ticketText } from "./detailFixtures";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { RunPasses, TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";

// 2026-10-13-9a59q: the ticket page as a reader meets it, driven through the page's one store.
// 2026-11-17-8d1ca: the section bar beside one section; ?section= names it, an older ?tab= lands on its section.

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

// 2026-11-16-6877a: the composer is on every ticket page now; its conversation list reads nothing here.
vi.mock("@/lib/specDialogApi", async (original) => ({
  ...(await original<typeof import("@/lib/specDialogApi")>()),
  fetchSpecDialogConversations: async () => ({ conversations: [], total: 0 }),
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
  it("SectionBar_ShowsOnlyRecordedFields", async () => {
    world.detail = detail([attempt()], { branch: "" });
    world.detail.summary = { ...world.detail.summary, platform: null, trackerState: null };
    await open();
    const head = screen.getByTestId("ticket-head");
    expect(head).toHaveTextContent("GH-101");
    expect(head).toHaveTextContent("Fix the login");
    expect(within(head).queryByRole("link")).toBeNull();
    expect(screen.getByTestId("ticket-back")).toHaveAttribute("href", "/tickets");
    expect(screen.queryByTestId("ticket-tracker-fact")).toBeNull();
    expect(screen.queryByTestId("ticket-branch")).toBeNull();
    expect(screen.getByTestId("ticket-pr-fate")).toHaveTextContent("no PR");
  });

  it("SectionBar_FactsNameProjectTrackerStatusAndBranch_TheTrackerLinkIsInTheMoreMenu", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, webUrl: "https://example.test/101", trackerState: "Active", platform: "github" };
    await open();
    const facts = screen.getByTestId("ticket-facts");
    expect(facts).toHaveTextContent("shop");
    expect(screen.getByTestId("ticket-tracker-fact")).toHaveTextContent("GitHub status Active");
    expect(screen.getByTestId("ticket-branch")).toHaveTextContent("agent-smith/GH-101");
    expect(screen.getByTestId("ticket-more-menu")).not.toBeVisible();
    fireEvent.click(screen.getByTestId("ticket-more"));
    expect(screen.getByTestId("ticket-more-menu")).toBeVisible();
    expect(screen.getByTestId("ticket-open-tracker")).toHaveAttribute("href", "https://example.test/101");
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.getByTestId("ticket-more-menu")).not.toBeVisible();
  });
  it("Header_FailedAttemptWithMergedPullRequests_IsOk_AndNamesBothFates", async () => {
    world.detail = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "done", landing: "merged",
      pullRequests: [pr({ state: "merged" }), pr({ number: "810", state: "merged" })] };
    await open();
    expect(screen.getByTestId("ticket-state-word")).toHaveClass("ok");
    expect(screen.getByTestId("ticket-state-word")).toHaveTextContent("Done");
    expect(screen.getByTestId("ticket-state-sentence")).toHaveTextContent("Done");
    expect(screen.getByTestId("ticket-pr-fate")).toHaveTextContent("2 PRs merged");
  });

  // 2026-10-16-86e6c: the state block under the header.
  it("TicketStateBlock_Failed_ShowsSentenceRunLinkActAndClosedWhy", async () => {
    world.detail = detail([attempt({ status: "failed" }), attempt({ number: 2, runId: "r-2", status: "failed",
      endKind: "criteria-unmet", summary: "1 criterion(s) of the ratified spec are not satisfied by the branch" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    await open();
    const block = screen.getByTestId("ticket-state");
    expect(block).toHaveAttribute("data-tone", "bad");
    expect(screen.getByTestId("ticket-state-sentence")).toHaveTextContent("Attempt 2 failed: the verifier of that time did not accept a criterion.");
    expect(screen.getByTestId("ticket-state-run")).toHaveAttribute("href", "/jobs/r-2");
    expect(screen.getByTestId("ticket-state-act")).not.toHaveAttribute("href", "/jobs/r-2");
    expect(screen.getByTestId("ticket-state-why")).not.toHaveAttribute("open");
    expect(screen.queryByText("Other options")).toBeNull();
  });

  it("YourMove_Why_HoldsTheVerbatimSummaryRunAndTraceLink_TheActFocusesTheComposer", async () => {
    const cause = "Sandbox pod agentsmith-sandbox-1 could not start: ";
    world.detail = detail([attempt({ status: "failed", summary: cause })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    await open();
    expect(screen.getByTestId("ticket-state-cause").textContent).toBe(cause);
    expect(screen.getByTestId("ticket-state-run")).toHaveAttribute("href", "/jobs/r-1");
    expect(screen.getByTestId("ticket-state-trace")).toHaveAttribute("href", "/jobs/r-1/why");
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Stopped");
    expect(screen.getByTestId("ticket-state-act")).toHaveTextContent("Reply below");
  });
  // 2026-11-17-d6fbd: the box reads the head attempt's end detail, so a refusal names its subject and its setting.
  it("YourMove_StuckWithAnEndDetail_NamesTheSubjectTheInstructionAndTheSetting", async () => {
    world.detail = detail([attempt({ status: "failed", endKind: "image-untrusted", endDetail: {
      subject: "Image node:22-bookworm (context default)", repos: ["web"], control: "Trusted image registries",
      setTo: "ghcr.io/, docker.io/library/", settingsPath: "/config/settings/sandbox" } })]);
    world.detail.summary = { ...world.detail.summary, bucket: "stuck" };
    await open();
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Stuck: Image node:22-bookworm (context default)");
    expect(screen.getByTestId("ticket-state-sentence")).toHaveTextContent("Trusted image registries");
    expect(screen.getByTestId("ticket-state-sentence")).toHaveTextContent("ghcr.io/, docker.io/library/");
    const act = screen.getByTestId("ticket-state-act");
    expect(act).toHaveTextContent("Open the setting");
    expect(act).toHaveAttribute("href", "/config/settings/sandbox");
  });

  it("TicketPage_EverySection_ShowsOnlyThatSection_AndAnOlderTabLandsOnItsSection", async () => {
    world.detail = detail([attempt({ status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    const cases: Array<[string, string, string]> = [
      ["", "happened", "What happened"], ["tab=summary", "happened", "What happened"], ["tab=conversation", "happened", "What happened"],
      ["section=specs", "specs", "Specs"], ["tab=acceptance", "acceptance", "Acceptance"], ["tab=attempts", "runs", "Runs"],
      ["section=runs", "runs", "Runs"], ["tab=cost", "cost", "Cost"], ["section=defaults", "defaults", "Defaults"],
    ];
    for (const [search, id, title] of cases) {
      nav.search = search;
      await open();
      expect(screen.getByTestId("ticket-section-title")).toHaveTextContent(title);
      expect(screen.getByTestId(`ticket-section-${id}`)).toHaveAttribute("aria-current", "page");
      if (id === "happened") {
        // 2026-11-17-8d1cb: the Your move line heads What happened, above the lines; the composer is at the bottom.
        const log = screen.getByRole("log", { name: "Conversation" });
        expect(screen.getByTestId("ticket-state").compareDocumentPosition(log) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
        expect(screen.getByTestId("conv-dock")).not.toContainElement(screen.getByTestId("ticket-state"));
      } else {
        expect(screen.queryByRole("log", { name: "Conversation" })).toBeNull();
        expect(screen.getByTestId(`ticket-pane-${id}`)).toBeInTheDocument();
      }
      cleanup();
    }
  });

  it("TicketPage_ASectionButton_WritesSectionAndDropsTheOlderTab", async () => {
    nav.search = "tab=specs&causeOf=r-1";
    await open();
    expect(screen.queryByRole("tablist")).toBeNull();
    expect(screen.getByTestId("ticket-section-runs")).toHaveTextContent("Runs1");
    fireEvent.click(screen.getByTestId("ticket-section-runs"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101?section=runs", { scroll: false });
    fireEvent.click(screen.getByTestId("ticket-section-happened"));
    expect(nav.replace).toHaveBeenLastCalledWith("/tickets/shop/101", { scroll: false });
  });

  it("Thread_EveryAttemptReports_WithItsRunLine", async () => {
    world.detail = detail([attempt({ status: "failed", endKind: "image-untrusted", summary: "Image not trusted" }),
      attempt({ number: 2, runId: "r-2", status: "cancelled", endKind: "superseded" }),
      attempt({ number: 3, runId: "r-3", status: "failed", summary: "Pod did not start" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    await open();
    // 2026-11-17-8d1cb: every run says how it ended and why in words, not only the newest; the newest is open.
    expect(screen.getByTestId("thread-report-1")).toHaveTextContent("failed · image not trusted · Image not trusted");
    expect(screen.getByTestId("thread-report-2")).toHaveTextContent("superseded · Run 2 was cancelled.");
    expect(screen.queryByTestId("thread-run-1")).toBeNull();
    expect(screen.getByTestId("thread-run-3")).toHaveAttribute("href", "/jobs/r-3");
    expect(screen.getByTestId("thread-run-3")).toHaveTextContent(/^Run 3 · .* · \$\d+\.\d\d · open the run ›$/);
  });
  it("TicketStateBlock_PullRequests_AreRowsWithShortRepoNumberStateAndLink", async () => {
    world.detail = detail([attempt({ status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed", pullRequests: [
      pr({ number: "12", url: "https://dev.example/org/_git/Sample.App.Server/pullrequest/12", repo: "Sample.App.Server", state: "open" }),
      pr({ number: "13", url: "https://dev.example/org/_git/Sample.App.Client/pullrequest/13", repo: "Sample.App.Client", state: "merged", decision: "approved" })] };
    await open();
    const rows = within(screen.getByTestId("ticket-state-prs")).getAllByRole("listitem");
    expect(rows[0]).toHaveTextContent("Server #12 open");
    expect(rows[1]).toHaveTextContent("Client #13 merged · approved");
    expect(within(rows[0]).getByRole("link")).toHaveAttribute("href", "https://dev.example/org/_git/Sample.App.Server/pullrequest/12");
    expect(within(rows[0]).getByRole("link")).toHaveAttribute("title", "Sample.App.Server");
    expect(screen.getAllByTestId("ticket-state-act")).toHaveLength(1);
  });

  // 2026-11-16-6877a: every secondary act sits behind "···".
  it("MoreMenu_HoldsDoneByHandCloseAndDeriveSpecsWithWhereTheSpecsGo", async () => {
    world.detail = detail([attempt({ status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    await open();
    const menu = screen.getByTestId("ticket-more-menu");
    expect(screen.getByTestId("ticket-more")).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(screen.getByTestId("ticket-more"));
    expect(screen.getByTestId("ticket-more")).toHaveAttribute("aria-expanded", "true");
    expect(within(menu).getByTestId("done-by-hand-101")).toBeVisible();
    expect(within(menu).getByTestId("ticket-close-101")).toBeVisible();
    expect(within(menu).getByTestId("derive-specs-mode")).toHaveDisplayValue("Store on the branch");
    expect(within(menu).getByTestId("derive-specs-start")).toBeVisible();
    fireEvent.click(screen.getByTestId("ticket-more"));
    expect(menu).not.toBeVisible();
  });

  it("MoreMenu_AClosedTicket_OffersReopen_AndNoDeriveSpecs", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "closed", kind: "by-hand",
      closed: { by: "h.operator", at: "2026-09-04T15:00:00Z", note: null, closedPullRequests: [], trackerClosed: false } };
    await open();
    const menu = screen.getByTestId("ticket-more-menu");
    expect(within(menu).queryByTestId("derive-specs")).toBeNull();
    expect(screen.getByTestId("ticket-state")).toHaveAttribute("data-tone", "quiet");
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Closed");
  });

  it("YourMove_AQuestionWaits_IsAmber_AndItsActBringsTheQuestionCardIntoView", async () => {
    const question = { questionId: "q", type: "FreeText", text: "Keep the outbox?", context: null, choices: [], defaultAnswer: null, askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: "2026-09-05T14:10:00Z" };
    world.detail = detail([attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: question })]);
    world.detail.summary = { ...world.detail.summary, bucket: "needs_you", kind: "question", pendingQuestion: { runId: "r-1", question } };
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    await open();
    expect(screen.getByTestId("ticket-state")).toHaveAttribute("data-tone", "amber");
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Your move: answer the question");
    expect(screen.getByTestId("thread-report-1")).toHaveTextContent("Run 1 is waiting for your answer.");
    scrolled.mockClear();
    fireEvent.click(screen.getByTestId("ticket-state-act"));
    expect(screen.getByTestId("ticket-state-act")).toHaveClass("primary");
    expect(scrolled.mock.instances[0]).toBe(screen.getByTestId("conv-question").parentElement);
  });

  it("YourMove_InReviewWithOneOpenPullRequest_ItsBrightActOpensThePullRequest", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "waiting_on_review", pullRequests: [pr({ state: "open" })] };
    await open();
    expect(screen.getByTestId("ticket-state")).toHaveAttribute("data-tone", "person");
    expect(screen.getByTestId("ticket-state-act")).toHaveAttribute("href", "https://example.test/pr/809");
    expect(screen.getByTestId("ticket-state-act")).toHaveTextContent("Review the pull request");
  });

  it("YourMove_Done_IsGreenWithNoAct_Running_IsQuiet", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "done", landing: "merged", pullRequests: [pr({ state: "merged" })] };
    await open();
    expect(screen.getByTestId("ticket-state")).toHaveAttribute("data-tone", "ok");
    expect(screen.queryByTestId("ticket-state-act")).toBeNull();
    cleanup();
    world.detail = detail([attempt({ status: "running", finishedAt: null })]);
    world.detail.summary = { ...world.detail.summary, bucket: "working" };
    await open();
    expect(screen.getByTestId("ticket-state")).toHaveAttribute("data-tone", "quiet");
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Working. Nothing for you to do.");
  });

  it("TicketHeader_NoLongerSaysTheAttemptStatusNorOffersContinue", async () => {
    world.detail = detail([attempt({ status: "failed" })]);
    world.detail.summary = { ...world.detail.summary, bucket: "failed" };
    await open();
    const head = screen.getByTestId("ticket-head");
    expect(head).not.toHaveTextContent("attempt 1 failed");
    expect(screen.queryByTestId("ticket-continue")).toBeNull();
  });

  it("Header_SucceededAttemptWithEveryPullRequestClosedUnmerged_IsBad", async () => {
    world.detail = detail([attempt()]);
    world.detail.summary = { ...world.detail.summary, bucket: "idle", lastOutcome: "success", landing: "unmerged",
      pullRequests: ["1", "2", "3"].map((number) => pr({ number, state: "closed" })) };
    await open();
    expect(screen.getByTestId("ticket-state-word")).toHaveClass("bad");
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
    expect(screen.getByTestId("ticket-state-word")).toHaveClass("queued");
    expect(screen.getByTestId("ticket-state-word")).toHaveTextContent("Working");
  });

  it("CancelAttempt_ShowsOnlyForALiveAttempt_OutsideTheMenu_AndReadsCancellingAfterARunCancelRequested", async () => {
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null })]);
    await open();
    const cancel = screen.getByTestId("cancel-run-r-2");
    expect(cancel).toHaveTextContent("Cancel run 2");
    expect(cancel).toBeVisible();
    expect(screen.getByTestId("ticket-more-menu")).not.toContainElement(cancel);
    act(() => {
      stream.events = [{ runId: "r-2", type: EventType.RunCancelRequested, timestamp: "t" } as RunEvent];
      stream.listeners.forEach((l) => l());
    });
    expect(screen.getByTestId("cancel-run-r-2")).toHaveTextContent("cancelling…");
  });

  it("CancelAttempt_IsAbsentWhenNoAttemptIsLive", async () => {
    await open();
    expect(screen.queryByText(/Cancel run/)).toBeNull();
  });

  it("TicketStateBlock_Handback_RendersMarkdownWithMentionAsText", async () => {
    world.detail = detail([attempt({ status: "waiting_for_input", handedBack: true, finishedAt: null })]);
    world.detail.summary = { ...world.detail.summary, bucket: "needs_you", kind: "handback",
      handback: { text: "## Handed back\n\n<a href=\"#\" data-vss-mention=\"version:2.0,abc\">@Ada Example</a> please decide.", at: "2026-09-04T14:30:00Z" } };
    await open();
    const shown = screen.getByTestId("ticket-state-detail");
    expect(shown).toHaveTextContent("@Ada Example please decide.");
    expect(shown.textContent).not.toContain("##");
    expect(shown.innerHTML).not.toContain("data-vss-mention");
  });

  it("ANewAttemptAppearsAfterThisTicketsTicketActivityChanged", async () => {
    const hub = await open();
    expect(screen.queryByTestId("thread-report-2")).toBeNull();
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", status: "queued", finishedAt: null })]);
    act(() => hub.runsChanged.emit("r-2"));
    await settle(3000);
    expect(screen.queryByTestId("thread-report-2")).toBeNull();
    act(() => hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" }));
    await settle(2000);
    await settle();
    expect(screen.getByTestId("thread-report-2")).toBeInTheDocument();
  });

  // 2026-10-13-8762w: the series strip, only for a real, budgeted series. 2026-11-17-8d1ca: it heads the Cost section.
  const defaultSeries = (over: Parameters<typeof budget>[0] | null) =>
    series("r-1", [spec()], { budget: over === null ? null : budget(over) });

  it("TicketPage_NoBudget_RendersNoStrip", async () => {
    nav.search = "section=cost";
    world.series.default = defaultSeries(null);
    await open();
    expect(screen.getByTestId("ticket-head")).toBeInTheDocument();
    expect(screen.queryByTestId("series-runway")).toBeNull();
  });

  it("TicketPage_BudgetOff_RendersNoStrip", async () => {
    nav.search = "section=cost";
    world.series.default = defaultSeries({ next: "hold", heldBecause: "off" });
    await open();
    expect(screen.queryByTestId("series-runway")).toBeNull();
  });

  it("TicketPage_ActivityNudge_RefreshesTheStrip", async () => {
    nav.search = "section=cost";
    world.series.default = defaultSeries({ next: "start" });
    const hub = await open();
    expect(screen.getByTestId("ticket-pane-cost")).toContainElement(screen.getByTestId("series-runway"));
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

  it("TicketPage_HeldOnDefault_SelectsTheDefaultsSection", async () => {
    nav.search = "section=cost";
    world.series.default = defaultSeries({ next: "hold", heldBecause: "default-pending" });
    await open();
    fireEvent.click(screen.getByTestId("series-go-defaults"));
    expect(nav.replace).toHaveBeenCalledWith("/tickets/shop/101?section=defaults", { scroll: false });
  });
});
