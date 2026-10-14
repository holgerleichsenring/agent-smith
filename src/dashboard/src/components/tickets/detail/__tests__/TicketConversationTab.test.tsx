import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeHub } from "../../__tests__/ticketFixtures";
import { attempt, conversation, defaults, detail, entry, reviews, series, spec } from "./detailFixtures";
import { ApiResponseError } from "@/lib/apiResponse";
import type { RunEvent } from "@/types/hub-events";
import type { SpecDialogQuestionPush, SpecDialogView } from "@/types/spec-dialog";
import type {
  AttemptCause, TicketApprovalPreview, TicketConversation, TicketDetail, TicketSeries,
} from "@/types/tickets";

// 2026-10-13-9a59r: the Conversation tab as an operator meets it — the thread grouped as 9a59h placed it,
// the composer that is the ticket's Work it out session (moving nothing until the first act), the
// approval effect from the preview, Retry unchanged and the pending run question.

const NONE: RunEvent[] = [];
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => NONE }));
const nav = vi.hoisted(() => ({ search: "tab=conversation", replace: vi.fn() }));
vi.mock("next/navigation", () => ({
  useSearchParams: () => new URLSearchParams(nav.search),
  useRouter: () => ({ replace: nav.replace, push: vi.fn() }),
  usePathname: () => "/tickets/shop/101",
}));

const dialogHub = vi.hoisted(() => {
  const subject = <T,>() => {
    const listeners = new Set<(value: T) => void>();
    return {
      add: (l: (value: T) => void) => { listeners.add(l); return () => listeners.delete(l); },
      emit: (value: T) => { for (const l of [...listeners]) l(value); },
    };
  };
  return {
    specDialogMessages: subject(), specDialogQuestions: subject<unknown>(), specDialogProposals: subject(),
    specDialogFilings: subject(), specDialogReadings: subject(), specDialogActivity: subject(),
    connectionState: subject(), subscribeSpecDialog: async () => async () => {},
  };
});
vi.mock("@/lib/JobsHubClient", () => ({ getJobsHubClient: () => dialogHub }));

const dialogApi = vi.hoisted(() => ({
  fetch: vi.fn(),
  post: vi.fn<(...args: unknown[]) => Promise<void>>(async () => {}),
  resume: vi.fn<(...args: unknown[]) => Promise<void>>(async () => {}),
}));
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (id: string) => dialogApi.fetch(id),
  fetchSpecDialogConversations: async () => ({ conversations: [], total: 0 }),
  postSpecDialogMessage: (...args: unknown[]) => dialogApi.post(...args),
  resumeSpecDialogConversation: (...args: unknown[]) => dialogApi.resume(...args),
  deleteSpecDialogConversation: async () => {},
  uploadSpecDialogImage: async () => ({}),
  uploadSpecDialogReferences: async () => ({}),
}));

const ticketsApi = vi.hoisted(() => ({ preview: vi.fn(), retry: vi.fn() }));
vi.mock("@/lib/ticketsApi", async (original) => ({
  ...(await original<typeof import("@/lib/ticketsApi")>()),
  fetchApprovalPreview: (...args: unknown[]) => ticketsApi.preview(...args),
  retryAttempt: (...args: unknown[]) => ticketsApi.retry(...args),
}));

const identity = vi.hoisted(() => ({ lacks: [] as string[] }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: (p: string) => identity.lacks.includes(p) }));

const { TicketPageStore } = await import("@/lib/tickets/TicketPageStore");
const { TicketPage } = await import("../TicketPage");

const world = {
  detail: null as unknown as TicketDetail,
  conversation: conversation([]) as TicketConversation,
  sessions: {} as Record<string, SpecDialogView["session"]>,
  questions: {} as Record<string, SpecDialogQuestionPush | null>,
};
const api = {
  detail: vi.fn(() => Promise.resolve(world.detail)),
  series: vi.fn<(project: string, ticketId: string, runId: string | null) => Promise<TicketSeries>>(
    () => Promise.resolve(series("r-1", [spec()]))),
  conversation: vi.fn(() => Promise.resolve(world.conversation)),
  passes: vi.fn(() => Promise.reject(new Error("unused"))),
  run: vi.fn(() => Promise.reject(new Error("unused"))),
  compute: vi.fn(() => Promise.reject(new Error("unused"))),
  reviews: vi.fn(() => Promise.resolve(reviews())),
  defaults: vi.fn(() => Promise.resolve(defaults())),
};

const settle = async (ms = 0) => {
  for (let i = 0; i < 4; i++) await act(() => vi.advanceTimersByTimeAsync(ms / 4));
};

async function open() {
  const { hub, client } = fakeHub();
  render(<TicketPageStore project="shop" ticketId="101" client={client} api={api}><TicketPage /></TicketPageStore>);
  await settle();
  return hub;
}

function dialogView(id: string): SpecDialogView {
  return {
    dialogId: id, session: world.sessions[id] ?? null, projects: [], question: world.questions[id] ?? null,
    turn: { computing: false, elapsedSeconds: 0, steps: [], turnStartedAt: null },
  };
}

function session(sessionId: string): SpecDialogView["session"] {
  return {
    sessionId, ticket: { ticketId: "101", title: "Fix the login", text: "", readAt: "2026-09-04T14:00:00Z", truncated: false },
    scope: { name: "shop", repos: [], templates: [] }, transcript: [], lastActivityAt: "2026-09-04T14:00:00Z",
    subject: null, proposal: null, filing: null, proposalTurn: null, images: [],
  };
}

function approvalQuestion(id: string, over: Partial<SpecDialogQuestionPush> = {}): SpecDialogQuestionPush {
  return { dialogId: id, questionId: "q-1", kind: "approval", text: "", choices: [], at: "2026-09-04T14:32:00Z", expiresAt: null, ...over };
}

function preview(over: Partial<TicketApprovalPreview> = {}): TicketApprovalPreview {
  return {
    project: "shop", ticketId: "101", effect: "continue", reason: "", executed: 1, unstarted: 0, basisRunId: "r-1",
    specs: [{ specId: "s1", goal: "Port the publishers", disposition: "kept" }], start: { kind: "start-now" }, ...over,
  };
}

const PROPOSAL = { kind: "spec", drafts: [{ specId: "s6", goal: "Import consumer cap 30 s", repos: ["portal-worker"], done: ["caps at 30 s"] }] };
const PR = "https://github.com/acme/portal/pull/813";
const prCause: AttemptCause = { channel: "pull_request", actor: "m.reviewer", source: { kind: "comment", url: PR, id: "c-77" } };

const input = () => screen.getByTestId("dialog-composer-text");
const sendText = async (text: string) => {
  fireEvent.change(input(), { target: { value: text } });
  fireEvent.click(screen.getByTestId("dialog-composer-send"));
  await settle();
  await settle();
};

beforeEach(() => {
  vi.useFakeTimers();
  nav.search = "tab=conversation";
  identity.lacks = [];
  world.detail = detail([attempt()]);
  world.conversation = conversation([]);
  world.sessions = {};
  world.questions = {};
  for (const fn of [...Object.values(api), dialogApi.post, dialogApi.resume]) fn.mockClear();
  dialogApi.fetch.mockReset();
  dialogApi.fetch.mockImplementation(async (id: string) => dialogView(id));
  ticketsApi.preview.mockReset();
  ticketsApi.preview.mockImplementation(async () => preview());
  ticketsApi.retry.mockReset();
});
afterEach(() => vi.useRealTimers());

describe("Ticket conversation tab", () => {
  it("OpeningTheTab_IssuesNoResumeNoMessageAndNoWrite_TheClosedSessionReadsFromTheTranscript", async () => {
    world.conversation = { ...conversation([entry({ id: "conversation:s-1:0", source: "conversation", kind: "turn", role: "person", group: "now", attemptRunId: null, text: "Add the import consumer" })]), sessionId: "s-1" };
    await open();
    expect(screen.getByTestId("conv")).toHaveTextContent("Add the import consumer");
    expect(dialogApi.resume).not.toHaveBeenCalled();
    expect(dialogApi.post).not.toHaveBeenCalled();
    expect(dialogApi.fetch.mock.calls.every(([id]) => id !== "s-1")).toBe(true);
  });

  it("TheFirstSend_ResumesTheSessionAndPosts", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-1" };
    dialogApi.resume.mockImplementation(async (_s: unknown, dialogId: unknown) => { world.sessions[dialogId as string] = session("s-1"); });
    await open();
    await sendText("Add the import consumer");
    expect(dialogApi.resume).toHaveBeenCalledTimes(1);
    const [sessionId, dialogId] = dialogApi.resume.mock.calls[0];
    expect(sessionId).toBe("s-1");
    expect(dialogApi.post).toHaveBeenCalledWith(dialogId, "Add the import consumer", "shop", undefined);
  });

  it.each([
    ["question-pending", "This conversation is waiting for an answer where it is open. Answer it there, then open the conversation here."],
    ["turn-running", "This conversation is in the middle of a turn. Open it here once that turn has replied."],
  ])("ARefusedResume_%s_RendersItsOwnState_NotAComposerError", async (kind, reason) => {
    world.conversation = { ...conversation([]), sessionId: "s-1" };
    dialogApi.resume.mockRejectedValue(new ApiResponseError("/api/spec-dialog/conversations/s-1/resume", 409, `HTTP 409 — ${reason}`, reason));
    await open();
    await sendText("Hello");
    expect(screen.getByTestId(`ticket-resume-${kind}`)).toBeInTheDocument();
    expect(screen.queryByTestId("ticket-composer-failure")).toBeNull();
    expect(dialogApi.post).not.toHaveBeenCalled();
    expect(input()).toBeInTheDocument();
  });

  it("ACallerWhoIsNotTheOwnerButHoldsDialogWrite_ResumesAClosedTicketSessionOnTheFirstAct", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-someone-else" };
    await open();
    expect(dialogApi.resume).not.toHaveBeenCalled();
    fireEvent.focus(input());
    await settle();
    expect(dialogApi.resume).toHaveBeenCalledWith("s-someone-else", expect.stringMatching(/^d-/));
  });

  it("ThePage_ReadsTheConversationAndSeriesThroughTheStoreOnly_OneRequestPerEndpointPerRefetch", async () => {
    const hub = await open();
    expect(api.conversation).toHaveBeenCalledTimes(1);
    expect(api.series.mock.calls.filter(([, , runId]) => runId === null)).toHaveLength(1);
    hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" });
    await settle(2100);
    expect(api.conversation).toHaveBeenCalledTimes(2);
    expect(api.series.mock.calls.filter(([, , runId]) => runId === null)).toHaveLength(2);
  });

  it("ARunsChangedBurst_IssuesNoConversationAndNoPreviewRead_ATicketActivityChanged_RefetchesThePreviewOnce", async () => {
    world.detail = detail([attempt({ status: "running", finishedAt: null })]);
    world.conversation = { ...conversation([]), sessionId: "s-1", openDialogId: "d-open", proposal: PROPOSAL };
    const hub = await open();
    expect(ticketsApi.preview).toHaveBeenCalledTimes(1);
    for (let i = 0; i < 5; i++) hub.runsChanged.emit("r-1");
    await settle(3000);
    expect(api.conversation).toHaveBeenCalledTimes(1);
    expect(ticketsApi.preview).toHaveBeenCalledTimes(1);
    for (let i = 0; i < 3; i++) hub.ticketActivityChanged.emit({ project: "shop", ticketKey: "GH-101" });
    await settle(2100);
    await settle(2100);
    expect(ticketsApi.preview).toHaveBeenCalledTimes(2);
  });

  it("NoPreviewRequest_WhileTheConversationShowsNoOpenProposal_OrIsHidden", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-1" };
    await open();
    expect(ticketsApi.preview).not.toHaveBeenCalled();
  });

  it("AHiddenConversation_DrawsNoComposerAndMakesNoPreviewRequest", async () => {
    world.conversation = { ...conversation([entry({ attemptRunId: "r-1" })]), proposal: PROPOSAL, transcriptHidden: true };
    world.conversation.attempts = [{ runId: "r-1", number: 1, cause: null, revision: null }];
    await open();
    expect(screen.queryByTestId("ticket-composer")).toBeNull();
    expect(screen.getByTestId("conv-hidden")).toHaveTextContent("needs dialog.write");
    expect(screen.getByTestId("conv")).toHaveTextContent("Keep the outbox?");
    expect(ticketsApi.preview).not.toHaveBeenCalled();
  });

  it("ANonEnforcingInstallWhoseIdentityLacksDialogWrite_StillGetsTheComposer", async () => {
    identity.lacks = ["dialog.write"];
    await open();
    expect(screen.getByTestId("ticket-composer")).toBeInTheDocument();
  });

  it("Entries_GroupUnderTheAttemptMarkersAndTheBeforeNowGroups_InTheReadsOrder", async () => {
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", status: "failed" })]);
    world.conversation = {
      ...conversation([
        entry({ id: "a", group: "before", attemptRunId: null, text: "before one" }),
        entry({ id: "b", attemptRunId: "r-1", text: "first attempt" }),
        entry({ id: "c", attemptRunId: "r-2", text: "second attempt" }),
        entry({ id: "d", attemptRunId: "r-2", text: "second attempt later" }),
        entry({ id: "e", source: "conversation", kind: "turn", role: "person", group: "now", attemptRunId: null, text: "now one" }),
      ]),
      attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }, { runId: "r-2", number: 2, cause: null, revision: null }],
    };
    await open();
    const groups = [...screen.getByTestId("conv").querySelectorAll("[data-testid^=conv-group-]")].map((g) => g.getAttribute("data-testid"));
    expect(groups).toEqual(["conv-group-before", "conv-group-r-1", "conv-group-r-2", "conv-group-now"]);
    const second = within(screen.getByTestId("conv-group-r-2"));
    expect(second.getByText("Attempt 2")).toBeInTheDocument();
    expect(second.getAllByTestId("conv-entry").map((e) => e.getAttribute("data-entry-id"))).toEqual(["c", "d"]);
    expect(screen.getByTestId("conv-group-now")).toHaveTextContent("in the ticket conversation");
  });

  it("CauseOf_ScrollsToTheEntryThatIsThatAttemptsCause_WithNoMatchItStaysAtTheTop", async () => {
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", cause: prCause })]);
    world.conversation = {
      ...conversation([
        entry({ id: "pr-review:k:c-76", source: "pr-review", kind: "comment", sourceId: "c-76", url: PR, attemptRunId: "r-1", role: "reviewer" }),
        entry({ id: "pr-review:k:c-77", source: "pr-review", kind: "comment", sourceId: "c-77", url: PR, attemptRunId: "r-1", role: "reviewer" }),
      ]),
      attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }, { runId: "r-2", number: 2, cause: prCause, revision: null }],
    };
    nav.search = "tab=conversation&causeOf=r-2";
    await open();
    expect(scrolled).toHaveBeenCalledTimes(1);
    expect((scrolled.mock.instances[0] as unknown as HTMLElement).getAttribute("data-entry-id")).toBe("pr-review:k:c-77");

    cleanup();
    scrolled.mockClear();
    nav.search = "tab=conversation&causeOf=r-1";
    await open();
    expect(scrolled).not.toHaveBeenCalled();
  });

  it("AWorkersLaterAnswerComment_ShowsUnderTheTicketChip_AsAnAgentEntry", async () => {
    world.conversation = {
      ...conversation([
        entry({ id: "ticket:55", source: "ticket", kind: "comment", sourceId: "55", role: "agent", isOurs: true, text: "Not a rework: the comment asks nothing of the code." }),
        entry({ id: "q", text: "Keep the outbox?" }),
      ]),
      attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }],
    };
    await open();
    fireEvent.click(screen.getByTestId("conv-filter-ticket"));
    const shown = screen.getAllByTestId("conv-entry");
    expect(shown).toHaveLength(1);
    expect(shown[0]).toHaveTextContent("Not a rework");
    expect(shown[0].querySelector(".av.agent")).not.toBeNull();
    expect(screen.getByTestId("conv-filter-ticket")).toHaveAttribute("aria-pressed", "true");
  });

  it("ThePrReviewsChip_ShowsOnlyReviewEntries_AReviewCommentShowsFileAndLine_ATicketEditShowsItsDiffAndNoEditor", async () => {
    world.conversation = {
      ...conversation([
        entry({ id: "t", source: "ticket", kind: "comment", sourceId: "9", role: "person", author: "h.operator", text: "Ready." }),
        entry({ id: "p", source: "pr-review", kind: "comment", sourceId: "c-1", url: PR, role: "reviewer", author: "m.reviewer", file: "Messaging/RetryPolicy.cs", line: 18, text: "Configurable per consumer.", detail: { decision: "changes-requested" } }),
        entry({ id: "e", source: "ticket", kind: "edited", role: "person", author: null, text: null, detail: { criteria: { before: "Retries back off.", after: "Retries back off: base 2 s." } } }),
      ]),
      attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }],
    };
    await open();
    fireEvent.click(screen.getByTestId("conv-filter-review"));
    const reviews = screen.getAllByTestId("conv-entry");
    expect(reviews.map((e) => e.getAttribute("data-source"))).toEqual(["pr-review"]);
    expect(within(reviews[0]).getByTestId("conv-file")).toHaveTextContent("Messaging/RetryPolicy.cs · line 18");
    expect(reviews[0]).toHaveTextContent("PR #813 · comment");
    expect(reviews[0]).toHaveTextContent("changes requested");

    fireEvent.click(screen.getByTestId("conv-filter-all"));
    const edit = screen.getAllByTestId("conv-entry").find((e) => e.getAttribute("data-entry-id") === "e")!;
    expect(edit).toHaveTextContent("Ticket text changed");
    expect(within(edit).getByTestId("conv-detail")).toHaveTextContent("− Retries back off.");
    expect(within(edit).getByTestId("conv-detail")).toHaveTextContent("+ Retries back off: base 2 s.");
    expect(edit).not.toHaveTextContent("h.operator");
  });

  it("AStartedAttemptFooter_AppearsOnlyOnTheEntryCauseEntryMatchJoinsToThatAttemptsCause", async () => {
    world.detail = detail([attempt(), attempt({ number: 2, runId: "r-2", cause: prCause })]);
    world.conversation = {
      ...conversation([
        entry({ id: "x", source: "pr-review", kind: "comment", sourceId: "c-77", url: PR, attemptRunId: "r-1", role: "reviewer" }),
        entry({ id: "y", source: "pr-review", kind: "comment", sourceId: "c-78", url: PR, attemptRunId: "r-1", role: "reviewer", at: "2026-09-04T14:21:00Z" }),
      ]),
      attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }, { runId: "r-2", number: 2, cause: prCause, revision: null }],
    };
    await open();
    const footers = screen.getAllByTestId("conv-started");
    expect(footers).toHaveLength(1);
    expect(footers[0]).toHaveTextContent("started attempt 2");
    expect(footers[0].closest("[data-entry-id]")).toHaveAttribute("data-entry-id", "x");
  });

  it("Approve_AnswersTheApprovalQuestionWithApprove_OnTheOpenDialog", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-1", openDialogId: "d-open", proposal: PROPOSAL };
    world.sessions["d-open"] = session("s-1");
    world.questions["d-open"] = approvalQuestion("d-open");
    await open();
    expect(screen.getByTestId("ticket-effect")).toHaveTextContent("Approving continues this ticket's series. The next attempt starts now.");
    expect(screen.getByTestId("ticket-proposal")).toHaveTextContent("Import consumer cap 30 s · portal-worker");
    fireEvent.click(screen.getByTestId("ticket-approve"));
    await settle();
    await settle();
    expect(dialogApi.resume).not.toHaveBeenCalled();
    expect(dialogApi.post).toHaveBeenCalledWith("d-open", "approve", "shop", undefined);
  });

  it("WithRefusedQuestionPending_ThereIsNoApprove", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-1", openDialogId: "d-open", proposal: PROPOSAL };
    ticketsApi.preview.mockImplementation(async () => preview({ start: { kind: "refused-question-pending" } }));
    await open();
    expect(screen.getByTestId("ticket-effect")).toHaveTextContent("answer it first");
    expect(screen.queryByTestId("ticket-approve")).toBeNull();
    expect(screen.getByTestId("ticket-discard")).toBeInTheDocument();
  });

  it("AnExpiredQuestion_OffersNoApprove_AndWithNoApprovalQuestionThereIsNoEffectLine", async () => {
    world.conversation = { ...conversation([]), sessionId: "s-1", openDialogId: "d-open", proposal: PROPOSAL };
    world.sessions["d-open"] = session("s-1");
    world.questions["d-open"] = approvalQuestion("d-open", { expiresAt: "2020-01-01T00:00:00Z" });
    await open();
    fireEvent.focus(input());
    await settle();
    await settle();
    expect(screen.queryByTestId("ticket-approve")).toBeNull();
    expect(screen.queryByTestId("ticket-effect")).toBeNull();
  });

  it("RetryUnchanged_AppearsOnlyAfterAnInfrastructureEnd_AndA409ShowsItsReasonText", async () => {
    world.detail = detail([attempt({ status: "failed", endedOnInfrastructure: true })]);
    ticketsApi.retry.mockResolvedValue({ kind: "refused", refusal: { reason: "closed-for-work", text: "the ticket was closed in its tracker — approving a change in the ticket conversation files a follow-up", project: "shop", ticketId: "101" } });
    await open();
    fireEvent.click(screen.getByTestId("ticket-retry"));
    await settle();
    expect(ticketsApi.retry).toHaveBeenCalledWith("r-1");
    expect(screen.getByTestId("ticket-retry-refused")).toHaveTextContent("approving a change in the ticket conversation files a follow-up");
  });

  it("RetryUnchanged_IsHiddenForANonInfrastructureEnd_AndFromACallerKnownToLackRunsControl", async () => {
    world.detail = detail([attempt({ status: "failed" })]);
    await open();
    expect(screen.queryByTestId("ticket-retry")).toBeNull();
    cleanup();
    identity.lacks = ["runs.control"];
    world.detail = detail([attempt({ status: "failed", endedOnInfrastructure: true })]);
    await open();
    expect(screen.queryByTestId("ticket-retry")).toBeNull();
  });

  it("WithoutRunsControl_ThePendingQuestionShowsAsTextWithNoAnswerCard", async () => {
    const question = { questionId: "q", type: "FreeText", text: "Keep the Postgres outbox?", context: null, choices: [], defaultAnswer: null, askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: "2026-09-05T14:10:00Z" };
    world.detail = detail([attempt({ status: "waiting_for_input", finishedAt: null, pendingQuestion: question })]);
    identity.lacks = ["runs.control"];
    await open();
    expect(screen.getByTestId("conv-question-text")).toHaveTextContent("Keep the Postgres outbox?");
    expect(screen.getByTestId("conv-question-text")).toHaveTextContent("answering needs runs.control");
    expect(screen.queryByTestId("pending-question-card")).toBeNull();
    expect(screen.getByTestId("ticket-tab-conversation").querySelector(".tc.hot")).not.toBeNull();
  });
});
