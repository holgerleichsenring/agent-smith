import { render, renderHook, screen, fireEvent, waitFor, cleanup, act, within } from "@testing-library/react";
import { HubConnectionState } from "@microsoft/signalr";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SpecDialogSurface } from "../SpecDialogSurface";
import { useSpecDialog } from "@/hooks/useSpecDialog";
import { __forgetDialogIdForTests } from "@/lib/specDialogSession";
import type {
  SpecDialogActivityPush,
  SpecDialogFilingPush,
  SpecDialogMessagePush,
  SpecDialogPhaseProposal,
  SpecDialogProposalPush,
  SpecDialogQuestionPush,
  SpecDialogReadingPush,
  SpecDialogSessionSummary,
  SpecDialogTurn,
  SpecDialogView,
  FiledWork,
  FiledWorkReview,
  FiledWorkTicket,
} from "@/types/spec-dialog";

// 2026-09-15-cb3e: the conversation surface over a faked channel — the API calls it makes
// and the hub pushes it renders. No SignalR and no server: what is under test is that a
// typed question grows the controls it says it needs, that the operator's own message
// lands in the transcript, that a reload stays in the same session, and that a framework
// line composed for this channel reads as formatting.

function makeSubject<T>() {
  const listeners = new Set<(value: T) => void>();
  return {
    add(listener: (value: T) => void) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    emit(value: T) {
      for (const listener of [...listeners]) listener(value);
    },
  };
}

const messages = makeSubject<SpecDialogMessagePush>();
const questions = makeSubject<SpecDialogQuestionPush>();
const proposals = makeSubject<SpecDialogProposalPush>();
const filings = makeSubject<SpecDialogFilingPush>();
const readings = makeSubject<SpecDialogReadingPush>();
const activity = makeSubject<SpecDialogActivityPush>();
const subscribeSpecDialog = vi.fn(async () => async () => {});
// 2026-09-17-042ej: the filed-work watch and its data-free nudge. The double grows with the
// client, because a surface that mounts this hook invokes both on every render of this file.
const filedWorkChanged = makeSubject<void>();
const connectionState = makeSubject<HubConnectionState>();
const watchFiledWork = vi.fn(async () => async () => {});


// 2026-09-21-f237b: the surface reads its own address now — ?open= hands it a conversation from
// the conversations page. The search params are a ref so a case can set them before rendering,
// and the replace that strikes the parameter afterwards is observable.
const searchParams = { current: new URLSearchParams() };
const routerReplace = vi.fn();
vi.mock("next/navigation", () => ({
  useSearchParams: () => searchParams.current,
  useRouter: () => ({ replace: routerReplace }),
  usePathname: () => "/spec-dialog",
}));

vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: messages,
    specDialogQuestions: questions,
    specDialogProposals: proposals,
    specDialogFilings: filings,
    specDialogReadings: readings,
    specDialogActivity: activity,
    filedWorkChanged,
    connectionState,
    subscribeSpecDialog,
    watchFiledWork,
  }),
}));

const fetchSpecDialog = vi.fn();
// 2026-09-20-4b0aa: the project rides on the post, so the server can open the conversation and
// route the message in one ordered act. The mock takes it so a test can say it travelled.
const postSpecDialogMessage =
  vi.fn<(dialogId: string, text: string, project?: string) => Promise<void>>(async () => {});
const fetchSpecDialogConversations = vi.fn();
const fetchFiledWork = vi.fn();
const deleteSpecDialogConversation = vi.fn<(sessionId: string) => Promise<void>>(async () => {});
// 2026-09-22-2a86: opening a past conversation is a ROUTE — the conversation by its session id,
// onto the dialog id this tab holds — and no longer a "/spec resume" posted as message text.
const resumeSpecDialogConversation =
  vi.fn<(sessionId: string, dialogId: string) => Promise<void>>(async () => {});
// 2026-09-20-3af8: the upload and the address the transcript reads an image back from.
const uploadSpecDialogImage =
  vi.fn<(dialogId: string, project: string, file: File) => Promise<unknown>>(
    async () => ({ id: 7, mediaType: "image/png", at: "2026-09-15T09:30:00Z" }));
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (dialogId: string) => fetchSpecDialog(dialogId),
  fetchSpecDialogConversations: (limit?: number) => fetchSpecDialogConversations(limit),
  fetchFiledWork: (dialogId: string) => fetchFiledWork(dialogId),
  postSpecDialogMessage: (dialogId: string, text: string, project?: string) =>
    postSpecDialogMessage(dialogId, text, project),
  deleteSpecDialogConversation: (sessionId: string) => deleteSpecDialogConversation(sessionId),
  resumeSpecDialogConversation: (sessionId: string, dialogId: string) =>
    resumeSpecDialogConversation(sessionId, dialogId),
  uploadSpecDialogImage: (dialogId: string, project: string, file: File) =>
    uploadSpecDialogImage(dialogId, project, file),
  specDialogImageUrl: (imageId: number) => `/api/spec-dialog/images/${imageId}`,
}));

const SAMPLE_SCOPE = {
  name: "sample",
  repos: ["repo-a"],
  templates: [{ name: "template:default", repo: "template-repo", revision: "v1.2" }],
};

function view(overrides: Partial<SpecDialogView> = {}): SpecDialogView {
  return {
    dialogId: "d-1",
    question: null,
    turn: { computing: false, elapsedSeconds: 0, steps: [], turnStartedAt: null },
    session: {
      sessionId: "s-1",
      scope: SAMPLE_SCOPE,
      transcript: [],
      lastActivityAt: "2026-09-15T10:00:00Z",
      subject: null,
      proposal: null,
      filing: null,
      proposalTurn: null,
      images: [],
    },
    projects: [SAMPLE_SCOPE],
    ...overrides,
  };
}

function question(overrides: Partial<SpecDialogQuestionPush> = {}): SpecDialogQuestionPush {
  return {
    dialogId: heldDialogId(),
    questionId: "q-1",
    kind: "approval",
    text: "File these two tickets?",
    choices: [],
    at: "2026-09-15T10:01:00Z",
    expiresAt: null,
    ...overrides,
  };
}

function phase(
  phaseId: string,
  overrides: Partial<SpecDialogPhaseProposal> = {},
): SpecDialogPhaseProposal {
  return {
    phaseId,
    goal: `goal of ${phaseId}`,
    steps: [`step of ${phaseId}`],
    tests: [`Test_Of_${phaseId}`],
    done: [`done of ${phaseId}`],
    requires: [],
    yaml: `phase: ${phaseId}`,
    ...overrides,
  };
}

function proposal(overrides: Partial<SpecDialogProposalPush> = {}): SpecDialogProposalPush {
  return {
    dialogId: heldDialogId(),
    kind: "phase",
    bug: null,
    phase: phase("p9001"),
    parent: null,
    children: [],
    at: "2026-09-15T10:03:00Z",
    findings: [],
    ...overrides,
  };
}

/** 2026-09-18-2f8b: the running turn's identity, and a later one for the turn after it. */
const TURN = "2026-09-18T10:00:00Z";
const NEXT_TURN = "2026-09-18T10:05:00Z";

/** 2026-09-18-2f8b: one step of a running turn, read or pushed — the two carry one shape. */
function step(overrides: Partial<SpecDialogActivityPush> = {}): SpecDialogActivityPush {
  return {
    dialogId: "d-1",
    kind: "tool",
    name: "read_file",
    detail: "repo-a/src/A.cs",
    at: "2026-09-18T10:00:00Z",
    seq: 1,
    turnStartedAt: TURN,
    ...overrides,
  };
}

function filing(overrides: Partial<SpecDialogFilingPush> = {}): SpecDialogFilingPush {
  return {
    dialogId: heldDialogId(),
    filed: [{ reference: "https://tracker/7", title: "p9001: the phase" }],
    error: null,
    at: "2026-09-15T10:04:00Z",
    notes: [],
    ...overrides,
  };
}

// 2026-09-17-042ej: what the filed-work read answers for the ticket the filing above created.
function filedWork(overrides: Partial<FiledWorkTicket> = {}): FiledWork {
  return {
    dialogId: heldDialogId(),
    tickets: [
      {
        reference: "https://tracker/7",
        key: "SAMPLE-412",
        title: "p9001: the phase",
        ticketId: "7",
        project: "sample",
        start: { state: "Started", reason: "it already triggers" },
        handback: null,
        runs: [
          {
            runId: "2026-09-17T09-00-00-0001",
            project: "sample",
            pipeline: "phase-execution",
            status: "running",
            costUsd: 2.5,
            startedAt: "2026-09-17T09:00:00Z",
            finishedAt: null,
            pendingQuestion: null,
            pullRequests: [
              {
                repo: "api",
                status: "opened",
                url: "https://git/pr/3",
                reason: null,
                openedAt: "2026-09-17T10:00:00Z",
              },
              // 2026-09-17-042ef: the two states no fixture carried — a raw column value with
              // an underscore in it, and the failure whose reason the row holds and nothing
              // was showing.
              {
                repo: "docs",
                status: "no_changes",
                url: null,
                reason: null,
                openedAt: "2026-09-17T10:01:00Z",
              },
              {
                repo: "infra",
                status: "failed",
                url: null,
                reason: "the branch was rejected by the remote",
                openedAt: "2026-09-17T10:02:00Z",
              },
            ],
            phases: [
              {
                phaseId: "p9001a",
                ordinal: 1,
                title: "Make the thing exist",
                status: "done",
                verdict: null,
                review: {
                  reviewed: true,
                  why: null,
                  unreadable: false,
                  findings: [
                    {
                      repository: "api",
                      path: "src/A.cs",
                      line: 4,
                      rule: "no silent catch",
                      why: "the catch body logs nothing",
                      cites: "P1",
                      reverted: "the fix pass was reverted: the suite stayed red",
                    },
                    // 2026-09-17-042ef: a second one, because one finding cannot show whether
                    // two run together — which on the rendered page they did.
                    // 2026-09-17-042ef: reverted too, because the revert note was keyed by
                    // PHASE and two of them under one phase made getByTestId throw.
                    {
                      repository: "api",
                      path: "src/B.cs",
                      line: 9,
                      rule: "no magic values",
                      why: "the retry count is a literal",
                      cites: "P4",
                      reverted: "the fix pass was reverted: the build stayed red",
                    },
                  ],
                },
              },
              {
                phaseId: "p9001b",
                ordinal: 2,
                title: "Make the thing readable",
                status: "in_progress",
                verdict: null,
                review: {
                  reviewed: false,
                  why: "the run's configured cost cap is exhausted",
                  findings: [],
                  unreadable: false,
                },
              },
              {
                phaseId: "p9001c",
                ordinal: 3,
                title: "Make the thing fast",
                status: "not_started",
                verdict: null,
                review: null,
              },
            ],
          },
        ],
        ...overrides,
      },
    ],
  };
}

/** The same filing, whose one phase stopped in the given status with the verdict it recorded. */
function stoppedPhase(status: string, verdict: string): FiledWork {
  const work = filedWork();
  const run = work.tickets[0].runs[0];
  return {
    ...work,
    tickets: [
      {
        ...work.tickets[0],
        runs: [
          {
            ...run,
            phases: [{ ...run.phases[0], status, verdict }],
          },
        ],
      },
    ],
  };
}

/** 2026-09-17-042ef: the same filing whose one phase carries the given review state. */
function reviewedBy(review: FiledWorkReview | null): FiledWork {
  const work = filedWork();
  const run = work.tickets[0].runs[0];
  return {
    ...work,
    tickets: [{
      ...work.tickets[0],
      runs: [{ ...run, phases: [{ ...run.phases[0], review }] }],
    }],
  };
}

/** The id the page minted and is holding — every push must carry it to be rendered. */
function heldDialogId(): string {
  return fetchSpecDialog.mock.calls.at(-1)?.[0] as string;
}

// This jsdom build ships no localStorage, and the held dialog id is the whole point of
// the reload case — so the test backs the browser's half with a Map.
function stubStorage(): void {
  const backing = new Map<string, string>();
  Object.defineProperty(window, "localStorage", {
    configurable: true,
    value: {
      getItem: (key: string) => backing.get(key) ?? null,
      setItem: (key: string, value: string) => {
        backing.set(key, value);
      },
      removeItem: (key: string) => {
        backing.delete(key);
      },
      clear: () => {
        backing.clear();
      },
    },
  });
}

beforeEach(() => {
  searchParams.current = new URLSearchParams();
  routerReplace.mockReset();
  stubStorage();
  __forgetDialogIdForTests();
  fetchSpecDialog.mockReset();
  fetchSpecDialog.mockResolvedValue(view());
  fetchSpecDialogConversations.mockReset();
  fetchSpecDialogConversations.mockResolvedValue(listing([]));
  postSpecDialogMessage.mockReset();
  fetchFiledWork.mockReset();
  fetchFiledWork.mockResolvedValue({ dialogId: "d-1", tickets: [] });
  subscribeSpecDialog.mockClear();
  watchFiledWork.mockClear();
  deleteSpecDialogConversation.mockReset();
  deleteSpecDialogConversation.mockResolvedValue(undefined);
  resumeSpecDialogConversation.mockReset();
  resumeSpecDialogConversation.mockResolvedValue(undefined);
});

afterEach(() => cleanup());

async function renderSurface() {
  render(<SpecDialogSurface />);
  await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());
  await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
}

// 2026-09-21-f237b: the list route answers a PAGE — the rows and the owner's whole count —
// because a page that shows a limit's worth of rows has to say so, and the row count cannot tell
// a full page from a caller who holds exactly that many. Every mocked answer is one.
function listing(conversations: SpecDialogSessionSummary[], total = conversations.length) {
  return { conversations, total };
}

describe("SpecDialogSurface", () => {
  it("SpecDialog_TheComposer_PutsTheTurnInTheTranscript", async () => {
    await renderSurface();

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "a widget that reads the ledger" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(
        heldDialogId(),
        "a widget that reads the ledger",
        "sample",
      ));
    expect(await screen.findByTestId("dialog-turn-user")).toHaveTextContent(
      "a widget that reads the ledger",
    );
  });

  it("SpecDialog_AReload_KeepsTheSession", async () => {
    await renderSurface();
    const first = heldDialogId();
    expect(screen.getByTestId("dialog-identity")).toHaveTextContent(first);

    cleanup();
    __forgetDialogIdForTests(); // a reload: the document forgets, the browser does not
    await renderSurface();

    expect(heldDialogId()).toBe(first);
    expect(screen.getByTestId("dialog-identity")).toHaveTextContent(first);
  });

  it("SpecDialog_ATypedQuestion_RendersItsChoicesAndAnsweringPostsThem", async () => {
    await renderSurface();

    act(() => questions.emit(question({
      kind: "choice",
      choices: [{ label: "the reader" }, { label: "the writer" }],
    })));

    fireEvent.click(await screen.findByTestId("dialog-answer-the reader"));

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "the reader", "sample"));
  });

  it("SpecDialog_TheApprovalGate_OffersApproveAndRejectThoughItCarriesNoChoices", async () => {
    await renderSurface();

    act(() => questions.emit(question()));

    fireEvent.click(await screen.findByTestId("dialog-answer-approve"));

    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "approve", "sample"));
  });

  // 2026-09-17-042el: a click on the gate is a decision, and it reads as one — live and after a reload.
  // The post is accepted before it is routed, so the click is pending until a later read confirms it.
  it("SpecDialog_ApprovingTheGate_ShowsADecisionNotAnOperatorMessage", async () => {
    await renderSurface();
    act(() => questions.emit(question()));

    fireEvent.click(await screen.findByTestId("dialog-answer-approve"));

    const pending = await screen.findByTestId("dialog-turn-decision");
    expect(pending.querySelector("[data-decision]")).toHaveAttribute("data-decision", "approved");
    expect(pending.querySelector("[data-decision]")).toHaveAttribute("data-pending", "true");
    expect(pending).not.toHaveTextContent("Approved");
    expect(screen.queryByTestId("dialog-turn-user")).toBeNull();

    const stored = view();
    stored.session!.transcript = [
      { role: "user", text: "approve", at: "2026-09-15T10:02:00Z", decision: "approved" },
    ];
    fetchSpecDialog.mockResolvedValue(stored);
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "Filed.", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(screen.getByTestId("dialog-turn-decision").querySelector("[data-decision]"))
        .not.toHaveAttribute("data-pending"));
    expect(screen.getAllByTestId("dialog-turn-decision")).toHaveLength(1);
    expect(screen.getByTestId("dialog-turn-decision")).toHaveTextContent("Approved");
    expect(screen.queryByTestId("dialog-turn-user")).toBeNull();
  });

  it("SpecDialog_AnApprovalTheServerDidNotRecord_IsDroppedAndTheQuestionComesBack", async () => {
    await renderSurface();
    act(() => questions.emit(question()));
    fireEvent.click(await screen.findByTestId("dialog-answer-approve"));
    await screen.findByTestId("dialog-turn-decision");
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();

    const unrecorded = view({ question: question() });
    unrecorded.session!.transcript = [
      { role: "user", text: "approve", at: "2026-09-15T10:02:00Z", decision: null },
    ];
    fetchSpecDialog.mockResolvedValue(unrecorded);
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "A turn is in progress.", at: new Date().toISOString(),
    }));

    expect(await screen.findByTestId("dialog-answer-approve")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-turn-decision")).toBeNull();
  });

  it("SpecDialog_ADecisionThisPageCannotName_ReadsAsTheMessageItWas", async () => {
    const held = view();
    held.session!.transcript = [
      { role: "user", text: "escalate", at: "2026-09-15T10:02:00Z", decision: "escalated" as never },
    ];
    fetchSpecDialog.mockResolvedValue(held);

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-turn-user")).toHaveTextContent("escalate");
    expect(screen.queryByTestId("dialog-turn-decision")).toBeNull();
  });

  it("SpecDialog_AReload_ShowsARecordedDecisionAsADecision", async () => {
    const held = view();
    held.session!.transcript = [
      { role: "user", text: "reject", at: "2026-09-15T10:02:00Z", decision: "rejected" },
      { role: "user", text: "and a question", at: "2026-09-15T10:03:00Z", decision: null },
    ];
    fetchSpecDialog.mockResolvedValue(held);

    render(<SpecDialogSurface />);

    const decision = await screen.findByTestId("dialog-turn-decision");
    expect(decision.querySelector("[data-decision]")).toHaveAttribute("data-decision", "rejected");
    expect(screen.getAllByTestId("dialog-turn-user")).toHaveLength(1);
    expect(screen.getByTestId("dialog-turn-user")).toHaveTextContent("and a question");
  });

  it("SpecDialog_ComposedFrameworkText_RendersAsFormattingNotAsPunctuation", async () => {
    await renderSurface();

    act(() => messages.emit({
      dialogId: heldDialogId(),
      title: "Spec dialog",
      text: "Spec dialog `s-1` opened — scope **sample** (repo-a).",
      at: "2026-09-15T10:02:00Z",
    }));

    const turn = await screen.findByTestId("dialog-turn-agent");
    expect(turn.querySelector("strong")).toHaveTextContent("sample");
    expect(turn.textContent).not.toContain("**");
  });

  // 2026-09-17-042ef: the page has a shell of its own. Under the runs list's, `section +
  // section` put a 26px top margin on the exchange column and dropped it below its two
  // neighbours; under the config studio's, .main caps at 1000px, narrower than the width the
  // third column appears at. Neither shell can be the one this page runs under.
  it("SpecDialog_TheSurface_RunsUnderItsOwnShellAndNeitherOfTheTwoItIsBuiltFrom", async () => {
    await renderSurface();

    const root = screen.getByTestId("spec-dialog");
    expect(root.className.split(/\s+/)).toContain("mock-shell");
    expect(root.className.split(/\s+/)).toContain("mock-dialog");
    expect(root.className.split(/\s+/)).not.toContain("mock-runs");
    expect(root.className.split(/\s+/)).not.toContain("mock-config");
  });

  // 2026-09-17-042ef: a repository and a template are each ONE mark, the Projects page's own.
  // A template says where it comes from after its name — "repo@revision", or a plain
  // repository where the scope pinned no revision.
  it("SpecDialog_TheScopeColumn_ShowsEachRepositoryAndEachTemplateAsAMark", async () => {
    const unpinned = {
      name: "unpinned",
      repos: ["repo-b"],
      templates: [{ name: "template:loose", repo: "loose-repo", revision: "" }],
    };
    fetchSpecDialog.mockResolvedValue(view({ session: null, projects: [SAMPLE_SCOPE, unpinned] }));
    await renderSurface();

    for (const repo of ["repo-a", "repo-b"]) {
      expect(screen.getByTestId(`dialog-scope-repo-${repo}`).className.split(/\s+/)).toContain("ec-mark");
    }
    const pinned = screen.getByTestId("dialog-scope-template-template:default");
    expect(pinned.className.split(/\s+/)).toContain("ec-mark");
    expect(pinned).toHaveTextContent("template-repo@v1.2");
    const loose = screen.getByTestId("dialog-scope-template-template:loose");
    expect(loose.className.split(/\s+/)).toContain("ec-mark");
    expect(loose).toHaveTextContent("loose-repo");
    expect(loose.textContent).not.toContain("@");
  });

  // 2026-09-18-e63d: a project, a repository and a template are all NAMED BY SOMEBODY ELSE,
  // and the pane they stand in is a fixed 360px track inside a card that clips. A name with no
  // space in it has no soft-wrap opportunity of its own — a template's is followed immediately
  // by repo@revision, with no text node between — so each carries the modifier that lets it
  // break anywhere. The HEADING is here too: it is 14.5px mono with no wrap help of its own, so
  // without the modifier it would be sliced while the marks one line below it wrapped cleanly.
  // Whether the text then READS in full is a rendered outcome no test in this project can
  // observe; what is asserted here is that each asks for it, and that its whole text is present.
  it("SpecDialog_TheScopeColumnsGivenText_AsksToWrap", async () => {
    const projectName = "a_project_name_with_no_break_opportunity";
    const repoName = "a-repository-name-with-no-space-in-it-at-all";
    const templateName = "template:a-name-nobody-here-chose";
    fetchSpecDialog.mockResolvedValue(view({
      session: null,
      projects: [{
        name: projectName,
        repos: [repoName],
        templates: [{ name: templateName, repo: "a-template-repository", revision: "v1.2" }],
      }],
    }));
    await renderSurface();

    const heading = screen.getByTestId(`dialog-scope-project-${projectName}`)
      .querySelector(".ec-name") as HTMLElement;
    expect(heading.className.split(/\s+/)).toContain("given");
    expect(heading.textContent).toBe(projectName);

    const repo = screen.getByTestId(`dialog-scope-repo-${repoName}`);
    expect(repo.className.split(/\s+/)).toContain("given");
    expect(repo.textContent).toBe(repoName);

    const template = screen.getByTestId(`dialog-scope-template-${templateName}`);
    expect(template.className.split(/\s+/)).toContain("given");
    expect(template.textContent).toBe(`${templateName}a-template-repository@v1.2`);
  });

  it("SpecDialog_TheScopeColumn_NamesTheRepositoriesAndTheTemplates", async () => {
    await renderSurface();

    const scope = screen.getByTestId("dialog-scope");
    expect(scope).toHaveTextContent("repo-a");
    expect(scope).toHaveTextContent("template:default");
    expect(scope).toHaveTextContent("v1.2");
  });

  // 2026-09-22-2a86: the control used to post "/spec other" as message text, which the server
  // parsed back into an opening command. It mints a tab now, and the FIRST message opens the
  // conversation on the picked project — one route, carrying what the page holds.
  it("Page_TheNewConversationControl_CallsTheRouteAndPostsNoCommandText", async () => {
    const other = { name: "other", repos: [], templates: [] };
    fetchSpecDialog.mockResolvedValue(view({ session: null, projects: [SAMPLE_SCOPE, other] }));
    await renderSurface();
    const first = heldDialogId();

    fireEvent.change(screen.getByTestId("dialog-project-picker"), {
      target: { value: "other" },
    });
    fireEvent.click(screen.getByTestId("dialog-new"));

    await waitFor(() => expect(heldDialogId()).not.toBe(first));
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "a widget that reads the ledger" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(
        heldDialogId(), "a widget that reads the ledger", "other"));
    expect(postSpecDialogMessage.mock.calls.map((call) => call[1]))
      .not.toContainEqual(expect.stringMatching(/^\/spec/));
  });

  function conversation(overrides: Partial<SpecDialogSessionSummary> = {}): SpecDialogSessionSummary {
    return {
      sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: "2026-09-15T09:00:00Z",
      title: "a widget that reads the ledger", subject: null, outcome: null, openDialogId: null,
      ...overrides,
    };
  }

  function turn(text: string): SpecDialogTurn {
    return { role: "user", text, at: "2026-09-15T09:00:00Z" };
  }

  // 2026-09-18-e63d: the likelier case of the same bug. This column is 220px — 140px narrower
  // than the scope pane — the title one line above it already needs truncation, and the project
  // name is the operator's own. The mark carries the same modifier.
  it("SpecDialog_AConversationsProjectMark_AsksToWrap", async () => {
    const projectName = "a-project-name-with-no-space-in-it-at-all";
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ project: projectName })]));
    await renderSurface();

    const mark = (await screen.findByTestId("dialog-conversation-s-9"))
      .querySelector(".ec-mark") as HTMLElement;
    expect(mark.className.split(/\s+/)).toContain("given");
    expect(mark.textContent).toBe(projectName);
  });

  it("SpecDialog_TheConversationList_NamesEachByTitleAndWhatItFiled", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ outcome: { kind: "epic", tickets: 3, partial: true } }),
    ]));
    await renderSurface();

    const row = await screen.findByTestId("dialog-conversation-s-9");
    expect(row).toHaveTextContent("a widget that reads the ledger");
    expect(row).toHaveTextContent("sample");
    expect(within(row).getByTestId("dialog-conversation-outcome")).toHaveTextContent(/^epic partly filed · 3 tickets$/);
  });

  // 2026-09-17-c7aed: the list reads as a history, by the calendar day of the last thing said.
  // 2026-09-21-f237c: ONE list under one heading, whatever days the rows fall on. The four day
  // headings cost a line each and carried only what a per-row timestamp carries, and they were
  // uneven by construction. This case keeps the aria-current assertions the grouping case held —
  // the negative half is the only one of its kind in the tree.
  it("SpecDialog_ConversationsFromAnyNumberOfDays_AreOneListUnderOneHeading", async () => {
    const now = new Date();
    const daysAgo = (days: number) =>
      new Date(now.getFullYear(), now.getMonth(), now.getDate() - days, 12).toISOString();
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", lastActivityAt: now.toISOString() }),
      conversation({ sessionId: "s-2", lastActivityAt: daysAgo(1) }),
      conversation({ sessionId: "s-3", lastActivityAt: daysAgo(3) }),
      conversation({ sessionId: "s-4", lastActivityAt: daysAgo(30) }),
    ]));
    await renderSurface();

    await screen.findByTestId("dialog-conversation-s-4");
    expect(screen.queryAllByTestId("dialog-conversation-day")).toEqual([]);
    const rows = [...screen.getByTestId("dialog-conversations")
      .querySelectorAll<HTMLElement>(".d-conv")];
    expect(rows.map((row) => row.dataset.testid)).toEqual([
      "dialog-conversation-s-1", "dialog-conversation-s-2",
      "dialog-conversation-s-3", "dialog-conversation-s-4",
    ]);
  });

  it("SpecDialog_TheConversations_MarkTheCurrentOne", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1" }),
      conversation({ sessionId: "s-2" }),
    ]));
    await renderSurface();

    await screen.findByTestId("dialog-conversation-s-2");
    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("dialog-conversation-s-2")).not.toHaveAttribute("aria-current");
  });

  // Every row says when it was last active, not only today's — that is what the day headings
  // were carrying for the older ones.
  it("SpecDialog_ARow_SaysWhenItWasLastActive", async () => {
    const now = new Date();
    const daysAgo = (days: number) =>
      new Date(now.getFullYear(), now.getMonth(), now.getDate() - days, 12).toISOString();
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-2", lastActivityAt: daysAgo(1) }),
      conversation({ sessionId: "s-3", lastActivityAt: daysAgo(3) }),
      conversation({ sessionId: "s-4", lastActivityAt: daysAgo(40) }),
    ]));
    await renderSurface();

    expect(await screen.findByTestId("dialog-conversation-s-2")).toHaveTextContent("yesterday");
    expect(screen.getByTestId("dialog-conversation-s-3")).toHaveTextContent("3 days ago");
    expect(screen.getByTestId("dialog-conversation-s-4")).toHaveTextContent("1mo ago");
  });

  // On the day you are working, the clock is what tells two conversations apart.
  it("SpecDialog_ARowFromToday_KeepsItsClockTime", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", lastActivityAt: new Date().toISOString() }),
    ]));
    await renderSurface();

    expect(await screen.findByTestId("dialog-conversation-s-1")).toHaveTextContent(/\d{1,2}:\d{2}/);
  });

  // Twenty is how many a person scans in a panel. The rest are a link away, since f237b.
  it("SpecDialog_MoreThanTwentyConversations_DrawsTwenty", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing(
      Array.from({ length: 30 }, (_unused, index) =>
        conversation({ sessionId: `s-${index}` }))));
    await renderSurface();

    await screen.findByTestId("dialog-conversation-s-0");
    expect(screen.getByTestId("dialog-conversations")
      .querySelectorAll(".d-conv")).toHaveLength(20);
    expect(screen.queryByTestId("dialog-conversation-s-25")).not.toBeInTheDocument();
  });

  // But never without the one being read: today every row is drawn, so the aria-current mark is
  // always on a row, and f237b made a twenty-first conversation very reachable.
  it("SpecDialog_AnOpenConversationBelowTheTwentieth_IsDrawnAsWell", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      ...Array.from({ length: 25 }, (_unused, index) =>
        conversation({ sessionId: `s-other-${index}` })),
      conversation({ sessionId: "s-1", title: "the one being read" }),
    ]));
    await renderSurface();

    expect(await screen.findByTestId("dialog-conversation-s-1"))
      .toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("dialog-conversations")
      .querySelectorAll(".d-conv")).toHaveLength(21);
  });

  // The cap is taken on the way to the screen and never on the array behind it: the predicate
  // and the pane heading both read that array, and a slice would make this conversation
  // unfindable in both.
  it("SpecDialog_AnOpenConversationBelowTheTwentieth_StillHeadsThePane", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      ...Array.from({ length: 25 }, (_unused, index) =>
        conversation({ sessionId: `s-other-${index}` })),
      conversation({ sessionId: "s-1", title: "the one being read" }),
    ]));
    await renderSurface();

    expect(await screen.findByTestId("dialog-heading"))
      .toHaveTextContent("the one being read");
  });

  it("SpecDialog_AnOpenConversationBelowTheTwentieth_TriggersNoFurtherListRead", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      ...Array.from({ length: 25 }, (_unused, index) =>
        conversation({ sessionId: `s-other-${index}`, turns: 2 })),
      conversation({ sessionId: "s-1", title: "the one being read", turns: 2 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;
    const read = fetchSpecDialog.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(fetchSpecDialog.mock.calls.length).toBeGreaterThan(read));
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(listed);
  });

  // A subject is minted short, but nothing guarantees it — and a row that grew to four lines for
  // one verbose subject would make a ragged column.
  it("SpecDialog_ALongSubject_IsClampedRatherThanTruncatedAtOneLine", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", subject: "a subject long enough to need a second line" }),
    ]));
    await renderSurface();

    const name = (await screen.findByTestId("dialog-conversation-s-1"))
      .querySelector("span");
    expect(name?.className).toContain("line-clamp-2");
    expect(name?.className).not.toContain("truncate");
  });

  it("SpecDialog_AConversationWithAnOutcome_ShowsIt_OneWithoutShowsNone", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-7", outcome: { kind: "bug", tickets: 1, partial: false } }),
      conversation({ sessionId: "s-8", outcome: null }),
    ]));
    await renderSurface();

    const filed = await screen.findByTestId("dialog-conversation-s-7");
    expect(within(filed).getByTestId("dialog-conversation-outcome")).toHaveTextContent("bug filed");
    expect(within(screen.getByTestId("dialog-conversation-s-8"))
      .queryByTestId("dialog-conversation-outcome")).toBeNull();
  });

  // The spec said every opening resumes onto a fresh dialog id; since 2026-09-17-c7aeb's review
  // only a CLOSED conversation does — an open one is returned to (the test below this one).
  it("Page_OpeningAConversationFromTheList_CallsTheRouteAndPostsNoCommandText", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ openDialogId: null })]));
    await renderSurface();
    const first = heldDialogId();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-9"));

    await waitFor(() => expect(resumeSpecDialogConversation).toHaveBeenCalled());
    const fresh = heldDialogId();
    expect(fresh).not.toBe(first);
    expect(resumeSpecDialogConversation.mock.calls).toEqual([["s-9", fresh]]);
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
  });

  // 2026-09-17-c7aeb: a dialog id is a tab, not a conversation. Opening a past one mints a
  // fresh id and resumes onto it, and that id's first read comes BEFORE the resume has moved
  // anything there — a re-seed spent on it left the page with no transcript at all.
  // Found by review: a conversation left mid-turn could not be reopened. The click was a resume,
  // a resume is refused while a turn runs, and the page had already left the tab the reply and a
  // waiting approval were going to. An open conversation is somewhere; the page goes there.
  it("SpecDialog_OpeningAnOpenConversation_ReturnsToItsDialogAndResumesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ openDialogId: "d-where-it-lives" })]));
    await renderSurface();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-9"));

    await waitFor(() => expect(heldDialogId()).toBe("d-where-it-lives"));
    expect(resumeSpecDialogConversation).not.toHaveBeenCalled();
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
  });

  // The list stands beside the conversation. A failed read of it used to put the page-wide
  // failure over a dialog that was working.
  it("SpecDialog_AFailedListRead_LeavesTheDialogWorking", async () => {
    fetchSpecDialogConversations.mockRejectedValue(new Error("list unavailable"));
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    await renderSurface();

    expect(await screen.findByTestId("dialog-composer-text")).toBeInTheDocument();
    await waitFor(() => expect(warn).toHaveBeenCalled());
    expect(screen.queryByTestId("failed-surface")).not.toBeInTheDocument();
    warn.mockRestore();
  });

  it("SpecDialog_OpeningAPastConversation_ShowsItsTranscriptAfterTheResume", async () => {
    let resumed = false;
    resumeSpecDialogConversation.mockImplementation(async () => {
      resumed = true;
    });
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation()]));
    await renderSurface();
    const first = heldDialogId();
    fetchSpecDialog.mockImplementation(async (dialogId: string) => {
      if (dialogId === first) return view();
      if (!resumed) return view({ dialogId, session: null });
      const base = view({ dialogId });
      return {
        ...base,
        session: {
          ...base.session!, sessionId: "s-9",
          transcript: [{ role: "user", text: "a widget that reads the ledger", at: "2026-09-15T09:00:00Z" }],
        },
      };
    });

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-9"));
    await waitFor(() => expect(heldDialogId()).not.toBe(first));
    const fresh = heldDialogId();
    await waitFor(() =>
      expect(resumeSpecDialogConversation).toHaveBeenCalledWith("s-9", fresh));

    // 2026-09-22-2a86: the route answers when the move is DONE, so the read that follows it is
    // what seeds the transcript. There is no framework reply to wait for any more.
    expect(await screen.findByTestId("dialog-turn-user")).toHaveTextContent(
      "a widget that reads the ledger",
    );
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
  });

  it("SpecDialog_ClickingTheConversationAlreadyOpen_DoesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-1" })]));
    await renderSurface();
    const first = heldDialogId();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-1"));
    await act(async () => {});

    expect(heldDialogId()).toBe(first);
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveAttribute("aria-current", "true");
  });

  // 2026-09-17-042em: the read the session opening triggers races the first append, so the
  // conversation the operator is IN was listed as untitled with zero turns for as long as the
  // conversation ran. The reply is sent after the turn is appended, so the read it triggers is
  // the first one that can see the turn — and the row it renders is the point, not the call.
  it("SpecDialog_AfterAReply_TheRunningConversationIsListedWithItsTitleAndTurns", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: null, turns: 0 }),
    ]));
    await renderSurface();
    expect(await screen.findByTestId("dialog-conversation-s-1")).toHaveTextContent("untitled s-1");

    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger", turns: 2 }),
    ]));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(screen.getByTestId("dialog-conversation-s-1")).toHaveTextContent(
        "a widget that reads the ledger"));
    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveTextContent("2 turns");
  });

  // One read goes out per framework message, so they overlap; without a sequence number of its
  // own the answer that arrives LAST wins, and that is not the same as the newest.
  it("SpecDialog_ALateConversationListResponse_DoesNotReplaceANewerOne", async () => {
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());

    let releaseStale: (page: ReturnType<typeof listing>) => void = () => {};
    fetchSpecDialogConversations.mockReturnValueOnce(
      new Promise<ReturnType<typeof listing>>((resolve) => { releaseStale = resolve; }));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "one", at: new Date().toISOString(),
    }));

    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "the newest title", turns: 4 }),
    ]));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "two", at: new Date().toISOString(),
    }));
    await waitFor(() =>
      expect(screen.getByTestId("dialog-conversation-s-1")).toHaveTextContent("the newest title"));

    await act(async () => releaseStale(
      listing([conversation({ sessionId: "s-1", title: "two replies ago", turns: 1 })])));

    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveTextContent("the newest title");
    expect(screen.queryByText("two replies ago")).not.toBeInTheDocument();
  });

  // 2026-09-17-042em, found in review: the read this phase added is the EXPENSIVE one — every
  // listed transcript parsed, two further JSON documents per row, up to fifty rows — so paying it
  // on every reply for the life of a conversation buys nothing once the row already names it.
  it("SpecDialog_TheConversationList_IsNotReadWhileTheRowAlreadyNamesTheConversation", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger", turns: 2 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;
    const read = fetchSpecDialog.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(fetchSpecDialog.mock.calls.length).toBeGreaterThan(read));
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(listed);
  });

  // 2026-09-20-4b0af: the heading says what the conversation is ABOUT. The subject rides the
  // SESSION, which this page re-reads after every message, so it is there on the read that
  // follows the first reply.
  // 2026-09-21-f237a: and the row beside it says the same thing, from its own read of the list.
  it("SpecDialog_AConversationWithASubject_HeadsWithIt", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, subject: "Das Widget, das das Hauptbuch liest" },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: "a widget that reads the ledger",
        subject: "Das Widget, das das Hauptbuch liest",
      }),
    ]));

    await renderSurface();

    expect(await screen.findByTestId("dialog-heading"))
      .toHaveTextContent("Das Widget, das das Hauptbuch liest");
    expect(screen.getByTestId("dialog-conversation-s-1"))
      .toHaveTextContent("Das Widget, das das Hauptbuch liest");
  });

  it("SpecDialog_AConversationWithout_HeadsWithTheFirstLineAsBefore", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger" }),
    ]));

    await renderSurface();

    await waitFor(() =>
      expect(screen.getByTestId("dialog-heading"))
        .toHaveTextContent("a widget that reads the ledger"));
  });

  // 2026-09-21-f237a: the column is 220px wide and truncates, so the opening sentence is the
  // part every conversation of a working session has in common. The row says what it is about.
  it("SpecDialog_ARowWithASubject_ShowsItInTheColumn", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: "Ich brauche alle libraries aktualisiert",
        subject: "Aktualisierung aller Projektbibliotheken",
      }),
    ]));

    await renderSurface();

    expect(await screen.findByTestId("dialog-conversation-s-1"))
      .toHaveTextContent("Aktualisierung aller Projektbibliotheken");
    expect(screen.getByTestId("dialog-conversation-s-1"))
      .not.toHaveTextContent("Ich brauche alle libraries aktualisiert");
  });

  // Nothing is backfilled, so every conversation older than the mint keeps the row it had.
  it("SpecDialog_ARowWithoutASubject_ShowsTheFirstLineAsBefore", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger", subject: null }),
    ]));

    await renderSurface();

    expect(await screen.findByTestId("dialog-conversation-s-1"))
      .toHaveTextContent("a widget that reads the ledger");
  });

  // The delete asks about the sentence the person WROTE, in the words they wrote it in — the
  // opposite preference to the row above it, over the same two strings.
  it("SpecDialog_ARowWithBoth_NamesTheFirstLineOnItsDeleteControl", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: "Ich brauche alle libraries aktualisiert",
        subject: "Aktualisierung aller Projektbibliotheken",
      }),
    ]));

    await renderSurface();

    expect(await screen.findByTestId("dialog-delete-s-1"))
      .toHaveAttribute("aria-label", "Delete Ich brauche alle libraries aktualisiert");
  });

  // A conversation opened with nothing but a pasted block has no first line at all. Asking about
  // "untitled s-1" beside a row that is showing its subject is worse than asking about the subject.
  it("SpecDialog_ARowWithASubjectAndNoTitle_NamesTheSubjectOnItsDeleteControl", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: null, subject: "Aktualisierung aller Projektbibliotheken",
      }),
    ]));

    await renderSurface();

    expect(await screen.findByTestId("dialog-delete-s-1"))
      .toHaveAttribute("aria-label", "Delete Aktualisierung aller Projektbibliotheken");
  });

  // The row is read from the LIST, which is re-read when the predicate says the page is behind —
  // so a conversation that was listed without a name says what it is about from the reply on.
  it("SpecDialog_AfterAReply_TheRowSaysWhatTheConversationIsAbout", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: null, subject: null, turns: 0 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());

    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: null, subject: "Aktualisierung aller Projektbibliotheken",
        turns: 2,
      }),
    ]));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    expect(await screen.findByTestId("dialog-conversation-s-1"))
      .toHaveTextContent("Aktualisierung aller Projektbibliotheken");
  });

  // The defect this phase closes: the title of a conversation opened with a fenced block is null
  // for GOOD, so before this the page paid the expensive read on every reply for its whole life,
  // while the row beside it had been naming it since the first turn.
  it("SpecDialog_ATitlelessRowThatGainedASubject_TriggersNoFurtherListRead", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({
        sessionId: "s-1", title: null, subject: "Aktualisierung aller Projektbibliotheken",
        turns: 2,
      }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;
    const read = fetchSpecDialog.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(fetchSpecDialog.mock.calls.length).toBeGreaterThan(read));
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(listed);
  });

  // A row with NEITHER is what the read exists to fix, and it still triggers one.
  it("SpecDialog_ATitlelessRowWithNoSubject_StillTriggersAListRead", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: null, subject: null, turns: 2 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(listed));
  });

  // The list read is the expensive one, and its predicate keys on the row having NO NAME AT ALL.
  // 2026-09-21-f237a: the subject joins it as a second way to be satisfied, never as a second
  // requirement — a conversation with a title and no subject is named, so it pays nothing, which
  // is what this case measures.
  it("SpecDialog_ASubjectlessConversation_DoesNotTriggerAFurtherListRead", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, subject: null, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger", turns: 2 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;
    const read = fetchSpecDialog.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(fetchSpecDialog.mock.calls.length).toBeGreaterThan(read));
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(listed);
  });

  // And it comes back the moment the count falls behind what the page has already read, so the
  // row never drifts more than an exchange from the conversation it names.
  it("SpecDialog_TheConversationList_IsReadAgainOnceTheRowFallsBehind", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-1", title: "a widget that reads the ledger", turns: 1 }),
    ]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(listed));
  });

  // 2026-09-18-7a05, rewritten by 2026-09-20-4b0ab: an operator clears a conversation they
  // started by mistake, and is told first what the deletion does NOT undo. This was seven spies
  // on window.confirm — the browser's own prompt, because no modal primitive existed — and it
  // is now a dialog this page draws, so the warning is read off the page instead of off the
  // argument a spy caught. The wording itself has a unit test of its own now
  // (conversationDelete.test.ts); what is pinned HERE is that the ask carries the warning this
  // conversation's filing earns, and that the answer given to it is acted on.
  function answer(decision: "confirm" | "cancel") {
    fireEvent.click(screen.getByTestId(`confirm-dialog-${decision}`));
  }

  /** Open the ask for a conversation with this outcome and read what it says, then clear the
   *  page again so the next outcome starts from nothing. */
  async function askedAbout(outcome: SpecDialogSessionSummary["outcome"]): Promise<string> {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-9", outcome })]));
    await renderSurface();
    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));
    const said = (await screen.findByTestId("confirm-dialog")).textContent ?? "";
    cleanup();
    __forgetDialogIdForTests();
    return said;
  }

  it("SpecDialog_DeletingAConversationWithTurns_AsksInThePage", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-9", turns: 3 })]));
    await renderSurface();

    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));

    // A dialog element on this page, not a prompt the browser named and drew.
    const asked = await screen.findByTestId("confirm-dialog");
    expect(asked.tagName).toBe("DIALOG");
    expect(asked).toHaveTextContent("Delete \u201Ca widget that reads the ledger\u201D?");
    // And the click that opened it deleted nothing on its own.
    expect(deleteSpecDialogConversation).not.toHaveBeenCalled();
  });

  // The three variants the removed spies asserted out of the browser prompt, now asserted out
  // of the rendered dialog: a phase (and the epic parent) names a sentence in the ticket that
  // stops resolving, a bug is not given a sentence it never had, and a filing whose kind is
  // gone falls back to the one true of every filing.
  it("SpecDialog_TheAsk_CarriesTheWarningForWhatThatConversationFiled", async () => {
    const phase = await askedAbout({ kind: "phase", tickets: 1, partial: false });
    expect(phase).toContain("Delete \u201Ca widget that reads the ledger\u201D?");
    expect(phase).toContain("This cannot be undone");
    expect(phase).toContain("The tickets it filed stay in the tracker");
    expect(phase).toContain("the specification approved here stays with them");
    expect(phase).toContain(
      "the sentence in the ticket that names this conversation will stop resolving");

    const bug = await askedAbout({ kind: "bug", tickets: 1, partial: false });
    expect(bug).toContain("The tickets it filed stay in the tracker");
    expect(bug).not.toContain("names this conversation");
    expect(bug).not.toContain("stop resolving");

    const noKind = await askedAbout({ kind: null, tickets: 2, partial: false });
    expect(noKind).toContain(
      "Whatever it filed stays in the tracker, and the map from this conversation to it stops"
      + " resolving.");
  });

  it("SpecDialog_CancellingTheAsk_DeletesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-9" })]));
    await renderSurface();
    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));
    await screen.findByTestId("confirm-dialog");

    answer("cancel");

    await waitFor(() => expect(screen.queryByTestId("confirm-dialog")).toBeNull());
    expect(deleteSpecDialogConversation).not.toHaveBeenCalled();
    // And the row it was asked about is still there.
    expect(screen.getByTestId("dialog-conversation-s-9")).toBeInTheDocument();
  });

  it("SpecDialog_ConfirmingTheAsk_DeletesThatConversation", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-9" }),
      conversation({ sessionId: "s-8", title: "another conversation" }),
    ]));
    await renderSurface();
    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));
    await screen.findByTestId("confirm-dialog");

    answer("confirm");

    await waitFor(() => expect(deleteSpecDialogConversation).toHaveBeenCalledWith("s-9"));
    // THAT conversation and no other: the ask carries which row it is about.
    expect(deleteSpecDialogConversation).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByTestId("confirm-dialog")).toBeNull());
  });

  it("SpecDialog_DeletingAnEmptyConversation_AsksNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-9", title: null, turns: 0 }),
    ]));
    await renderSurface();

    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));

    await waitFor(() => expect(deleteSpecDialogConversation).toHaveBeenCalledWith("s-9"));
    expect(screen.queryByTestId("confirm-dialog")).toBeNull();
  });

  // The list is re-read on a reply while the listed row is behind, and that read's sequence guard
  // drops a read superseded by a NEWER one — not one issued BEFORE the delete and landing after.
  it("SpecDialog_AListReadInFlightWhenTheDeleteLands_DoesNotBringTheRowBack", async () => {
    // The conversation open here is not listed at all, so every reply re-reads the list.
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-9" })]));
    await renderSurface();
    await screen.findByTestId("dialog-conversation-s-9");

    let releaseStale: (page: ReturnType<typeof listing>) => void = () => {};
    fetchSpecDialogConversations.mockReturnValueOnce(
      new Promise<ReturnType<typeof listing>>((resolve) => { releaseStale = resolve; }));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    fireEvent.click(screen.getByTestId("dialog-delete-s-9"));
    await screen.findByTestId("confirm-dialog");
    answer("confirm");
    await waitFor(() => expect(screen.queryByTestId("dialog-conversation-s-9")).toBeNull());

    await act(async () => releaseStale([conversation({ sessionId: "s-9" })]));

    expect(screen.queryByTestId("dialog-conversation-s-9")).toBeNull();
  });

  // The held dialog id now names a dead thread, so the surface leaves it: every pane is cleared
  // and a fresh id is minted, in the one act the new-conversation switch already performs.
  it("SpecDialog_DeletingTheOpenConversation_EmptiesEveryPaneAndChangesTheHeldDialogId", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("a widget that reads the ledger")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([conversation({ sessionId: "s-1" })]));
    await renderSurface();
    const first = heldDialogId();
    act(() => proposals.emit(proposal()));
    expect(await screen.findByTestId("dialog-proposal")).toBeInTheDocument();
    fetchSpecDialog.mockImplementation(async (dialogId: string) =>
      view({ dialogId, session: null }));

    fireEvent.click(await screen.findByTestId("dialog-delete-s-1"));
    await screen.findByTestId("confirm-dialog");
    answer("confirm");

    await waitFor(() => expect(deleteSpecDialogConversation).toHaveBeenCalledWith("s-1"));
    await waitFor(() => expect(heldDialogId()).not.toBe(first));
    await waitFor(() => expect(screen.queryByTestId("dialog-turn-user")).toBeNull());
    expect(screen.queryByTestId("dialog-proposal")).toBeNull();
  });

  // The row was a button, and a control nested in a button is invalid markup that warns. The two
  // controls are siblings, so the delete needs no propagation trick and the row never opens.
  it("SpecDialog_TheDeleteControl_DoesNotOpenTheConversation", async () => {
    fetchSpecDialogConversations.mockResolvedValue(listing([
      conversation({ sessionId: "s-9", openDialogId: null }),
    ]));
    await renderSurface();
    const first = heldDialogId();

    fireEvent.click(await screen.findByTestId("dialog-delete-s-9"));
    await screen.findByTestId("confirm-dialog");
    answer("confirm");

    await waitFor(() => expect(deleteSpecDialogConversation).toHaveBeenCalledWith("s-9"));
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
    expect(heldDialogId()).toBe(first);
  });

  // The filing's own read went: the filing NOTICE is a framework message, so the list follows it
  // like every other reply — one mechanism, not two.
  it("SpecDialog_TheFilingNotice_RereadsTheConversationList", async () => {
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;

    act(() => filings.emit(filing()));
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(listed);

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "Filed one phase:", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(listed));
  });

  // 2026-09-21-f237b: the conversations page hands a conversation to this surface by its address.
  // Opening one is a hook callback and never was an address, so the address hands it to the hook.
  it("SpecDialog_AnAddressCarryingASessionId_OpensThatConversation", async () => {
    searchParams.current = new URLSearchParams({ open: "s-42" });

    await renderSurface();

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(
        expect.any(String), "/spec resume s-42", undefined));
  });

  // With the dialog id the row carried, it GOES there instead — which is the only reason the
  // address carries one. A resume is refused while a turn runs, and a person who left a
  // conversation mid-turn could otherwise not get back to its reply.
  it("SpecDialog_AnAddressCarryingAnOpenConversation_GoesToItsDialogRatherThanResuming", async () => {
    searchParams.current = new URLSearchParams({ open: "s-42", on: "d-7" });

    await renderSurface();

    await waitFor(() => expect(heldDialogId()).toBe("d-7"));
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
  });

  // Consumed once and struck from the address, so a reload does not reopen what the operator has
  // since navigated away from.
  it("SpecDialog_AnAddressConsumedOnce_DoesNotReopenOnTheNextRender", async () => {
    searchParams.current = new URLSearchParams({ open: "s-42", on: "d-7" });

    await renderSurface();

    await waitFor(() => expect(routerReplace).toHaveBeenCalledWith("/spec-dialog"));
  });

  // 2026-09-21-f237b: once the page above can open a conversation the panel's read does not
  // contain, an absent row would otherwise answer "behind" on every reply for ever — the
  // expensive read as a poll, which is what the predicate exists to prevent.
  it("SpecDialog_AConversationWithNoRowInTheList_ReadsTheListOnceAndThenStops", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    // A CAPPED page that does not hold this conversation: fewer rows than the owner has.
    fetchSpecDialogConversations.mockResolvedValue(
      listing([conversation({ sessionId: "s-other", turns: 2 })], 63));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "one", at: new Date().toISOString(),
    }));
    await waitFor(() => expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(0));
    const spent = fetchSpecDialogConversations.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "two", at: new Date().toISOString(),
    }));
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "three", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());
    expect(fetchSpecDialogConversations).toHaveBeenCalledTimes(spent);
  });

  // But a conversation the server simply does not hold YET — the read that races its own
  // creation — must still get its read, or it would never reach the panel at all. A page that
  // served everything the owner has is authoritative; a capped one is not.
  it("SpecDialog_ANewConversationNothingHasListedYet_StillReadsTheList", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: { ...view().session!, transcript: [turn("one"), turn("two")] },
    }));
    fetchSpecDialogConversations.mockResolvedValue(listing([]));
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "a reply", at: new Date().toISOString(),
    }));

    await waitFor(() =>
      expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(listed));
  });

  // 2026-09-17-042em: the picker beside the list is the choice for a NEW conversation, so it can
  // say nothing about the one that is open. The header does.
  it("SpecDialog_TheExchangeHeader_NamesTheOpenSessionsProject", async () => {
    await renderSurface();

    expect(await screen.findByTestId("dialog-exchange-project")).toHaveTextContent("sample");
  });

  // A REGRESSION GUARD, not a discriminating test: with no session open there is no project to
  // name, and this passed before the header line existed because the element did not either. It
  // is kept so a later "always show the project" cannot quietly claim one for no conversation.
  it("SpecDialog_NoSessionOpen_TheHeaderNamesNoProject", async () => {
    fetchSpecDialog.mockResolvedValue(view({ session: null }));
    await renderSurface();

    expect(screen.queryByTestId("dialog-exchange-project")).not.toBeInTheDocument();
  });

  // Also a REGRESSION GUARD: this passes on the parent commit too, because the phase deliberately
  // left the picker alone. It is what says the header naming the session's project did NOT make
  // the picker follow it — the alternative this phase considered and rejected.
  it("SpecDialog_NewConversation_StillUsesThePickedProjectWhileASessionIsOpen", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      projects: [SAMPLE_SCOPE, { name: "other", repos: ["repo-b"], templates: [] }],
    }));
    await renderSurface();

    fireEvent.change(await screen.findByTestId("dialog-project-picker"), {
      target: { value: "other" },
    });
    fireEvent.click(screen.getByTestId("dialog-new"));
    fireEvent.change(await screen.findByTestId("dialog-composer-text"), {
      target: { value: "a widget that reads the ledger" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(
        expect.any(String), "a widget that reads the ledger", "other"));
  });

  // 2026-09-15-cb3e, found by review: nothing is pushed when a wait expires — the confirmer
  // stops waiting and clears its entry. A card that kept its button turned a click into an
  // ordinary message and bought a whole design turn on the word "approve".
  it("SpecDialog_AnExpiredQuestion_OffersNoButtonAndSaysItIsNotWaiting", async () => {
    await renderSurface();

    act(() => questions.emit(question({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })));

    const card = await screen.findByTestId("dialog-question");
    expect(card).toHaveTextContent("no longer waiting");
    expect(screen.queryByTestId("dialog-answer-approve")).toBeNull();
  });

  it("SpecDialog_AQuestionWithTimeLeft_StillOffersItsControls", async () => {
    await renderSurface();

    act(() => questions.emit(question({
      expiresAt: new Date(Date.now() + 600_000).toISOString(),
    })));

    expect(await screen.findByTestId("dialog-answer-approve")).toBeInTheDocument();
  });

  // The question reaches the page as a hub push and lives nowhere else, so before the read
  // carried it a refresh during the gate left a blocked master and an empty page.
  it("SpecDialog_AReloadDuringTheGate_TakesTheQuestionBackFromTheRead", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      question: {
        dialogId: "any", questionId: "q-9", kind: "approval",
        text: "File these two tickets?", choices: [], at: "2026-09-15T10:01:00Z",
        expiresAt: new Date(Date.now() + 600_000).toISOString(),
      },
    }));

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-question"))
      .toHaveTextContent("File these two tickets?");
    expect(screen.getByTestId("dialog-answer-approve")).toBeInTheDocument();
  });

  // The hub refuses a dialog id the caller does not own. The page must say so rather than
  // render as a live conversation that will never receive a reply.
  it("SpecDialog_ARefusedSubscription_SaysSoInsteadOfLookingLive", async () => {
    subscribeSpecDialog.mockRejectedValueOnce(new Error("not yours"));

    render(<SpecDialogSurface />);

    await waitFor(() => expect(screen.queryByTestId("dialog-composer")).toBeNull());
  });

  // 2026-09-15-cb3e, found in use: a design turn materialises the scope's repositories and
  // reads them — a minute of silence is normal, and the channel pushes no progress. The
  // first question the page produced was "is anything happening?".
  it("SpecDialog_WhileATurnRuns_ShowsThatItIsWorking", async () => {
    await renderSurface();

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    expect(await screen.findByTestId("dialog-working")).toBeInTheDocument();
  });

  it("SpecDialog_WhenTheAnswerArrives_StopsShowingThatItIsWorking", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    await screen.findByTestId("dialog-working");

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog",
      text: "here is what I would build", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(screen.queryByTestId("dialog-working")).toBeNull());
  });

  // 2026-09-17-c7aec: the working line names what the turn is reading — one line per
  // repository at its latest state, a failure included, so no line spins forever.
  it("SpecDialog_WhileATurnReads_ShowsEachOpenedRepositoryAndItsState", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    await screen.findByTestId("dialog-working");

    const at = new Date().toISOString();
    act(() => {
      readings.emit({ dialogId: heldDialogId(), repo: "repo-a", state: "opening", at });
      readings.emit({ dialogId: heldDialogId(), repo: "template-repo", state: "opening", at });
      readings.emit({ dialogId: heldDialogId(), repo: "repo-a", state: "ready", at });
      readings.emit({ dialogId: heldDialogId(), repo: "template-repo", state: "failed", at });
      readings.emit({ dialogId: "someone-else", repo: "foreign", state: "opening", at });
    });

    const lines = await screen.findAllByTestId("dialog-reading");
    expect(lines.map((line) => [line.textContent, line.getAttribute("data-state")])).toEqual([
      ["repo-a: ready", "ready"],
      ["template-repo: could not be opened", "failed"],
    ]);
  });

  // 2026-09-17-042ee: and what it DOES with them — newest last, so the eye stays at the
  // bottom where the next line arrives, with everything older folded away.
  it("SpecDialog_WhileATurnRuns_ListsWhatItDoesNewestLast", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    await screen.findByTestId("dialog-working");

    const at = new Date().toISOString();
    act(() => {
      activity.emit({ dialogId: heldDialogId(), kind: "tool", name: "read_file", detail: "repo-a/src/A.cs", at, seq: 1, turnStartedAt: TURN });
      activity.emit({ dialogId: heldDialogId(), kind: "tool", name: "grep_in_files", detail: "Dispatch", at, seq: 2, turnStartedAt: TURN });
      activity.emit({ dialogId: heldDialogId(), kind: "model", name: "sample-model", detail: "Checking the router.", at, seq: 3, turnStartedAt: TURN });
      activity.emit({ dialogId: heldDialogId(), kind: "reviewing", name: null, detail: null, at, seq: 4, turnStartedAt: TURN });
      activity.emit({ dialogId: "someone-else", kind: "tool", name: "read_file", detail: "foreign", at, seq: 1, turnStartedAt: TURN });
    });

    const shown = await screen.findByTestId("dialog-activity");
    expect(within(shown).getAllByTestId("dialog-activity-line").map((line) => line.textContent)).toEqual([
      "grep_in_files Dispatch",
      "thinking — Checking the router.",
      "reviewing its own proposal against the code",
    ]);
    const folded = screen.getByTestId("dialog-activity-folded");
    expect(folded).toHaveTextContent("1 earlier step");
    expect(within(folded).getAllByTestId("dialog-activity-line").map((line) => line.textContent)).toEqual([
      "read_file repo-a/src/A.cs",
    ]);
  });

  // 2026-09-18-2f8b: a page that did NOT post the message — opened on a second screen, or
  // reloaded mid-turn — issues this read and nothing else until the reply lands. Without the
  // turn on the view it shows a conversation that looks finished while it is running.
  it("SpecDialog_ArrivingDuringAComputingTurn_ShowsTheWorkingLineAndItsSteps", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      turn: {
        computing: true,
        elapsedSeconds: 12,
        steps: [step({ seq: 1, name: "read_file", detail: "repo-a/src/A.cs" })],
        turnStartedAt: TURN,
      },
    }));

    await renderSurface();

    const working = await screen.findByTestId("dialog-working");
    expect(within(working).getAllByTestId("dialog-activity-line").map((l) => l.textContent))
      .toEqual(["read_file repo-a/src/A.cs"]);
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("12s · 1 step");
  });

  // The page that posted issues NO read until the reply lands, so a working line rendered
  // only from the view would go dark for the whole turn it just started.
  it("SpecDialog_ThePageThatPosted_KeepsItsWorkingLineWhenTheViewSaysNothing", async () => {
    await renderSurface();

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    expect(await screen.findByTestId("dialog-working")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("0s · 0 steps");
  });

  it("SpecDialog_AStepThatWasReadAndThenPushed_IsShownOnce", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      turn: { computing: true, elapsedSeconds: 3, steps: [step({ seq: 1 })], turnStartedAt: TURN },
    }));
    await renderSurface();
    await screen.findByTestId("dialog-working");

    act(() => activity.emit({ ...step({ seq: 1 }), dialogId: heldDialogId() }));

    const lines = await screen.findAllByTestId("dialog-activity-line");
    expect(lines).toHaveLength(1);
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("1 step");
  });

  it("SpecDialog_AStepPushedWhileTheReadWasInFlight_IsNotLost", async () => {
    // The arriving page's read is still out when the turn reports its next step, so that
    // step is in no read at all. Merging only the read's way would drop it.
    let land!: (answered: SpecDialogView) => void;
    fetchSpecDialog.mockImplementation(
      () => new Promise<SpecDialogView>((resolve) => { land = resolve; }));
    const { result } = renderHook(() => useSpecDialog());
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    const dialogId = result.current.dialogId!;
    act(() => activity.emit({ ...step({ seq: 2, name: "grep_in_files" }), dialogId }));

    await act(async () => {
      land(view({
        turn: { computing: true, elapsedSeconds: 3, steps: [step({ seq: 1 })], turnStartedAt: TURN },
      }));
    });

    expect(result.current.activity.map((held) => held.seq)).toEqual([1, 2]);
  });

  it("SpecDialog_TwoIdenticalStepsInOneTurn_AreShownAsTwo", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    await screen.findByTestId("dialog-working");

    const at = new Date().toISOString();
    act(() => {
      activity.emit({ ...step({ seq: 1 }), dialogId: heldDialogId(), at });
      activity.emit({ ...step({ seq: 2 }), dialogId: heldDialogId(), at });
    });

    expect(await screen.findAllByTestId("dialog-activity-line")).toHaveLength(2);
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("2 steps");
  });

  // The ring is behind a motion variant, and under jsdom a media variant is not observable at
  // all — so what is asserted is that the RENDERED TEXT moves on its own.
  it("SpecDialog_AdvancingTheClock_ChangesTheElapsedCounterAndTheStepCount", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      fetchSpecDialog.mockResolvedValue(view({
        turn: { computing: true, elapsedSeconds: 5, steps: [step({ seq: 1 })], turnStartedAt: TURN },
      }));
      await renderSurface();
      const pulse = await screen.findByTestId("dialog-working-pulse");
      expect(pulse).toHaveTextContent("5s · 1 step");

      await act(async () => {
        vi.advanceTimersByTime(3000);
      });
      expect(pulse).toHaveTextContent("8s · 1 step");

      act(() => activity.emit({ ...step({ seq: 2 }), dialogId: heldDialogId() }));
      expect(pulse).toHaveTextContent("8s · 2 steps");
    } finally {
      vi.useRealTimers();
    }
  });

  // A design turn's own question blocks INSIDE the turn and that wait has no deadline: a
  // working line beside the card would tell the person the agent is thinking about the answer
  // they have not given yet.
  it("SpecDialog_ATurnWaitingOnAQuestion_ShowsNoWorkingLine", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      question: question({ dialogId: "d-1", kind: "free_text", text: "which repository?" }),
      turn: { computing: false, elapsedSeconds: 0, steps: [], turnStartedAt: null },
    }));

    await renderSurface();

    expect(await screen.findByTestId("dialog-question")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-working")).toBeNull();
  });

  // A reconnect loses every push sent during the gap, the reply that ends the turn included.
  // Without the read on the way back the page counts up for a turn that finished while the
  // laptop slept — and the next turn's steps meet a list that outlived it.
  it("SpecDialog_TheNextTurnsSteps_ReplaceTheDeadTurnsWhenTheReplyWasMissed", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      turn: {
        computing: true,
        elapsedSeconds: 8,
        steps: [step({ seq: 1 }), step({ seq: 2, name: "grep_in_files" })],
        turnStartedAt: TURN,
      },
    }));
    await renderSurface();
    await screen.findByTestId("dialog-working");

    act(() => activity.emit({
      ...step({ seq: 1, name: "read_file", detail: "repo-b/src/B.cs", turnStartedAt: NEXT_TURN }),
      dialogId: heldDialogId(),
    }));

    const lines = await screen.findAllByTestId("dialog-activity-line");
    expect(lines.map((line) => line.textContent)).toEqual(["read_file repo-b/src/B.cs"]);
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("1 step");
  });

  it("SpecDialog_TheHubReconnecting_RereadsTheConversation", async () => {
    await renderSurface();
    const reads = fetchSpecDialog.mock.calls.length;

    act(() => connectionState.emit(HubConnectionState.Connected));

    await waitFor(() => expect(fetchSpecDialog.mock.calls.length).toBeGreaterThan(reads));
  });

  // A tab that was already open when the turn started never reads again on its own: the
  // computing flag comes off a read, and the reply is what triggers the next one. Its first
  // step is the evidence a turn is running.
  it("SpecDialog_ATabOpenedBeforeThePost_LearnsFromTheFirstStepThatATurnIsRunning", async () => {
    await renderSurface();
    expect(screen.queryByTestId("dialog-working")).toBeNull();
    fetchSpecDialog.mockResolvedValue(view({
      turn: { computing: true, elapsedSeconds: 2, steps: [step({ seq: 1 })], turnStartedAt: TURN },
    }));

    act(() => activity.emit({ ...step({ seq: 1 }), dialogId: heldDialogId() }));

    expect(await screen.findByTestId("dialog-working")).toBeInTheDocument();
  });

  // The kept list is bounded, so a count of what the PAGE holds freezes past that bound and
  // one of the two liveness cues dies for the rest of the turn. The sequence is the true count.
  it("SpecDialog_ATurnPastTheKeptBound_StillCountsEveryStepItTook", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    await screen.findByTestId("dialog-working");

    act(() => {
      for (let seq = 1; seq <= 60; seq += 1) {
        activity.emit({ ...step({ seq }), dialogId: heldDialogId() });
      }
    });

    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("60 steps");
  });

  // The ring is behind a motion variant; the two cues beside it must not be. Under jsdom a
  // class applies no styles, so what is asserted is the CLASS NAMES from the pulse up to the
  // working line — a `motion-` token added there would hide the cue for the reader who asked
  // for less motion and leave every rendering test green.
  it("SpecDialog_TheLiveCues_CarryNoMotionVariant", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      turn: { computing: true, elapsedSeconds: 5, steps: [step({ seq: 1 })], turnStartedAt: TURN },
    }));
    await renderSurface();
    const working = await screen.findByTestId("dialog-working");

    const classes: string[] = [];
    for (let at: Element | null = screen.getByTestId("dialog-working-pulse"); at; at = at.parentElement) {
      classes.push(at.className);
      if (at === working) break;
    }

    expect(classes.length).toBeGreaterThan(1);
    expect(classes.filter((name) => name.includes("motion-"))).toEqual([]);
  });

  // A payload from a server that does not carry the turn yet must leave the page working.
  it("SpecDialog_AViewWithoutTheTurn_IsNotAPageWideFailure", async () => {
    const withoutTurn: Partial<SpecDialogView> = { ...view() };
    delete withoutTurn.turn;
    fetchSpecDialog.mockResolvedValue(withoutTurn as SpecDialogView);

    await renderSurface();

    expect(await screen.findByTestId("dialog-composer-text")).toBeInTheDocument();
    expect(screen.queryByTestId("failed-surface")).toBeNull();
    expect(screen.queryByTestId("dialog-working")).toBeNull();
  });

  it("SpecDialog_WhenTheAnswerArrives_ForgetsWhatTheTurnDid", async () => {
    const { result } = renderHook(() => useSpecDialog());
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    await act(() => result.current.send("update every dependency"));
    const dialogId = result.current.dialogId!;
    act(() => activity.emit({
      dialogId, kind: "revising", name: null, detail: null, at: new Date().toISOString(), seq: 1,
      turnStartedAt: TURN,
    }));
    expect(result.current.activity).toHaveLength(1);

    act(() => messages.emit({ dialogId, title: "Spec dialog", text: "done", at: new Date().toISOString() }));

    expect(result.current.activity).toEqual([]);
  });

  // The indicator hides its lines once the turn is answered anyway, so only the hook's own
  // state shows whether the answer let the turn's repositories go.
  it("SpecDialog_WhenTheAnswerArrives_ForgetsTheTurnsRepositories", async () => {
    const { result } = renderHook(() => useSpecDialog());
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    await act(() => result.current.send("update every dependency"));
    const dialogId = result.current.dialogId!;
    act(() => readings.emit({ dialogId, repo: "repo-a", state: "ready", at: new Date().toISOString() }));
    expect(result.current.readings).toHaveLength(1);

    act(() => messages.emit({ dialogId, title: "Spec dialog", text: "done", at: new Date().toISOString() }));

    expect(result.current.readings).toEqual([]);
  });

  // 2026-09-20-4b0aa: this used to be TWO posts — an opening command, then the message — and
  // awaiting the first proved only that its background task had started, because the route
  // answers before the turn runs. The project now rides on the one post and the server opens
  // and routes in that order.
  it("SpecDialog_AFirstMessage_IsPostedOnceAndNotPrecededByACommand", async () => {
    fetchSpecDialog.mockResolvedValue(view({ session: null }));
    render(<SpecDialogSurface />);
    await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() => expect(postSpecDialogMessage).toHaveBeenCalled());
    expect(postSpecDialogMessage.mock.calls)
      .toEqual([[heldDialogId(), "update every dependency", "sample"]]);
  });

  /// The sentence that went missing. The reply's read carries the transcript as the server has it
  /// stored, and a page that armed a reseed for the opening post replaced the whole exchange with
  /// it — wiping the locally echoed turn, which no later read ever puts back. With one act there
  /// is nothing to reseed for, so the read settles and leaves the turn alone.
  it("SpecDialog_AFirstMessage_SurvivesTheReadThatFollowsTheReply", async () => {
    fetchSpecDialog.mockResolvedValue(view({ session: null }));
    render(<SpecDialogSurface />);
    await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    expect(await screen.findByTestId("dialog-turn-user"))
      .toHaveTextContent("update every dependency");

    // The conversation exists now, and the read that follows the reply lags the turn that
    // opened it: what it carries has no record of the sentence yet.
    fetchSpecDialog.mockResolvedValue(view());
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "on it", at: new Date().toISOString(),
    }));

    await waitFor(() => expect(screen.getByTestId("dialog-transcript")).toHaveTextContent("on it"));
    expect(screen.getByTestId("dialog-turn-user")).toHaveTextContent("update every dependency");
  });

  // The other side of the same send: a conversation already open still posts once and still
  // echoes once — the project travelling with it opens nothing.
  it("SpecDialog_AMessageWithASessionOpen_IsStillEchoedOnce", async () => {
    await renderSurface();

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() => expect(postSpecDialogMessage).toHaveBeenCalled());
    expect(postSpecDialogMessage.mock.calls)
      .toEqual([[heldDialogId(), "update every dependency", "sample"]]);
    expect(await screen.findAllByTestId("dialog-turn-user")).toHaveLength(1);
  });

  it("SpecDialog_APhaseProposal_RendersGoalStepsTestsAndDone", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal()));

    const pane = await screen.findByTestId("dialog-proposal");
    expect(pane).toHaveAttribute("data-kind", "phase");
    expect(pane).toHaveTextContent("goal of p9001");
    expect(pane).toHaveTextContent("step of p9001");
    expect(pane).toHaveTextContent("Test_Of_p9001");
    expect(pane).toHaveTextContent("done of p9001");
    expect(screen.queryByTestId("dialog-scope")).not.toBeInTheDocument();
  });

  it("SpecDialog_ABugProposal_RendersTitleAndBody", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal({
      kind: "bug",
      phase: null,
      bug: { title: "Widget drops", body: "It drops.\n\n## Acceptance criteria\nIt stops." },
    })));

    const pane = await screen.findByTestId("dialog-proposal-bug");
    expect(pane).toHaveTextContent("Widget drops");
    expect(pane).toHaveTextContent("It drops.");
    expect(pane).toHaveTextContent("It stops.");
  });

  it("SpecDialog_AnEpicProposal_RendersParentAndChildrenInFilingOrder", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal({
      kind: "epic",
      phase: null,
      parent: phase("p9000"),
      // As the backend ordered them: the orderer the filer itself runs put b before a.
      children: [phase("p9000b"), phase("p9000a", { requires: ["p9000b"] })],
    })));

    const parent = await screen.findByTestId("dialog-proposal-phase-p9000");
    expect(parent).toHaveTextContent("goal of p9000");
    // 2026-09-17-0e79d: the parent draft is filed as the phase-labelled WORK ticket carrying the
    // whole approved set; calling it a record said the opposite of what the filer does.
    expect(parent).toHaveTextContent("The work ticket — one run works every slice");
    expect(parent).not.toHaveTextContent("a record, not work");
    // ":scope > li" is the slice list itself; a plain "li" would also collect the steps
    // and tests rendered inside each slice.
    const listed = [
      ...screen.getByTestId("dialog-proposal-children").querySelectorAll(":scope > li"),
    ];
    expect(listed.map((item) => item.textContent?.slice(0, 6))).toEqual(["p9000b", "p9000a"]);
  });

  it("SpecDialog_AnAnswerTurn_LeavesThePaneUnchanged", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal()));
    await screen.findByTestId("dialog-proposal");
    // The server keeps the proposal beside the push, so the read after the answer still holds it.
    reloadedWith({ proposal: proposal() });

    // An answer turn delivers a reply and no proposal — it proposes nothing.
    act(() => messages.emit({
      dialogId: heldDialogId(),
      title: "Spec dialog",
      text: "The ledger is written by the master, not the funnel.",
      at: "2026-09-15T10:05:00Z",
    }));

    expect(await screen.findByTestId("dialog-turn-agent")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-proposal")).toHaveTextContent("goal of p9001");
  });

  it("SpecDialog_ASupersedingProposal_ReplacesTheOneBeingDiscussed", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal()));
    await screen.findByTestId("dialog-proposal");

    act(() => proposals.emit(proposal({ phase: phase("p9002") })));

    const pane = await screen.findByTestId("dialog-proposal");
    expect(pane).toHaveTextContent("goal of p9002");
    expect(pane).not.toHaveTextContent("goal of p9001");
  });

  it("SpecDialog_WhatWasFiled_ShowsEachReferenceAndTitle", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal()));

    act(() => filings.emit(filing()));

    const pane = await screen.findByTestId("dialog-filed");
    expect(pane).toHaveTextContent("p9001: the phase");
    expect(pane.querySelector("a")).toHaveAttribute("href", "https://tracker/7");
    expect(screen.queryByTestId("dialog-proposal")).not.toBeInTheDocument();
  });

  // 2026-09-17-042em: what a person calls the ticket is the KEY, and the url is what sits behind
  // it. A list of web urls differing in their last few digits names nothing anyone can repeat.
  it("SpecDialog_AFiledTicket_IsLinkedByItsKeyAndTitle", async () => {
    await renderSurface();

    act(() => filings.emit(filing({
      filed: [{ reference: "https://tracker/7", title: "p9001: the phase", key: "SAMPLE-412" }],
    })));

    const link = (await screen.findByTestId("dialog-filed")).querySelector("a")!;
    expect(link).toHaveAttribute("href", "https://tracker/7");
    expect(link).toHaveTextContent("SAMPLE-412");
    expect(link).toHaveTextContent("p9001: the phase");
    expect(link).not.toHaveTextContent("https://tracker/7");
  });

  it("SpecDialog_AFilingWithoutAKey_StillReadsByItsReference", async () => {
    await renderSurface();

    act(() => filings.emit(filing()));

    const link = (await screen.findByTestId("dialog-filed")).querySelector("a")!;
    expect(link).toHaveTextContent("https://tracker/7");
    expect(link).toHaveTextContent("p9001: the phase");
  });

  it("SpecDialog_APartialFailure_ShowsWhatWasCreatedBesideTheError", async () => {
    await renderSurface();

    act(() => filings.emit(filing({
      filed: [{ reference: "https://tracker/1", title: "p9000: the cut" }],
      error: "the tracker refused the second slice",
    })));

    const pane = await screen.findByTestId("dialog-filed");
    expect(pane).toHaveTextContent("p9000: the cut");
    expect(screen.getByTestId("dialog-filed-error")).toHaveTextContent(
      "the tracker refused the second slice",
    );
  });

  // 2026-09-17-042ea: a filing that could not link a child to its parent says so.
  it("SpecDialog_AFilingNote_IsShownOnTheFiledPanel", async () => {
    await renderSurface();

    act(() => filings.emit(filing({
      notes: ["https://tracker/8 is not linked to its parent https://tracker/7: refused"],
    })));

    await screen.findByTestId("dialog-filed");
    expect(screen.getByTestId("dialog-filed-notes")).toHaveTextContent(
      "https://tracker/8 is not linked to its parent https://tracker/7: refused",
    );
    expect(screen.queryByTestId("dialog-filed-error")).not.toBeInTheDocument();
  });

  // 2026-09-17-042eg: the filed tab says what each ticket BECAME, not only that it exists.
  it("SpecDialog_TheFiledTab_MarksEachTicketStartedNotStartedOrRecord", async () => {
    await renderSurface();

    act(() => filings.emit(filing({
      filed: [
        {
          reference: "https://tracker/7",
          title: "p9000: the work",
          ticketId: "7",
          project: "proj",
          start: { state: "Started", reason: "moved into the trigger status 'To Do'." },
        },
        {
          reference: "https://tracker/8",
          title: "p9001: a bug",
          ticketId: "8",
          project: "proj",
          start: { state: "NotStarted", reason: "nothing would route it: project 'proj' ..." },
        },
        {
          reference: "https://tracker/9",
          title: "p9000a: a slice",
          ticketId: "9",
          project: "proj",
          start: { state: "Record", reason: "a record of a slice, not work" },
        },
      ],
    })));

    await screen.findByTestId("dialog-filed");
    expect(screen.getByTestId("dialog-filed-start-https://tracker/7")).toHaveTextContent(
      "started — moved into the trigger status 'To Do'.",
    );
    expect(screen.getByTestId("dialog-filed-start-https://tracker/8")).toHaveTextContent(
      "not started — nothing would route it",
    );
    expect(screen.getByTestId("dialog-filed-start-https://tracker/9")).toHaveTextContent(
      "record — a record of a slice, not work",
    );
  });

  // A filing written before that phase carries no start state; the panel says nothing about it
  // rather than guessing, which is the claim the state exists to stop.
  it("SpecDialog_AFilingWithoutStartStates_RendersAsBefore", async () => {
    await renderSurface();

    act(() => filings.emit(filing()));

    expect(await screen.findByTestId("dialog-filed")).toHaveTextContent("p9001: the phase");
    expect(screen.queryByTestId("dialog-filed-start-https://tracker/7")).not.toBeInTheDocument();
  });

  it("SpecDialog_AFilingWithoutNotes_StillShowsTheFiledPanel", async () => {
    await renderSurface();
    const withoutNotes = filing();
    delete withoutNotes.notes;

    act(() => filings.emit(withoutNotes));

    expect(await screen.findByTestId("dialog-filed")).toHaveTextContent("p9001: the phase");
    expect(screen.queryByTestId("dialog-filed-notes")).not.toBeInTheDocument();
  });

  it("SpecDialog_AProposalAfterAFiling_MovesTheColumnBackToWhatIsBeingDecided", async () => {
    await renderSurface();
    act(() => filings.emit(filing()));
    await screen.findByTestId("dialog-filed");

    act(() => proposals.emit(proposal({ phase: phase("p9002") })));

    expect(await screen.findByTestId("dialog-proposal")).toHaveTextContent("goal of p9002");
    expect(screen.queryByTestId("dialog-filed")).not.toBeInTheDocument();
  });

  // 2026-09-17-c7aea: the pane's state is kept on the session, so a reload takes it back.
  function reloadedWith(held: Partial<SpecDialogView["session"] & object>) {
    const base = view();
    fetchSpecDialog.mockResolvedValue({ ...base, session: { ...base.session!, ...held } });
  }

  it("SpecDialog_AReloadAfterAFiling_TakesTheFilingBackFromTheRead", async () => {
    reloadedWith({
      proposal: proposal({ at: "2026-09-15T10:03:00Z" }),
      filing: filing({ at: "2026-09-15T10:04:00Z" }),
    });

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-filed")).toHaveTextContent("p9001: the phase");
  });

  it("SpecDialog_AReloadWithAProposal_OffersItsRawForm", async () => {
    reloadedWith({ proposal: proposal({ phase: phase("p9001", { yaml: "phase: p9001\ngoal: g" }) }) });

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-proposal-raw-p9001")).toHaveTextContent("goal: g");
  });

  it("SpecDialog_AReloadWithAProposalNewerThanTheFiling_ShowsTheProposal", async () => {
    reloadedWith({
      proposal: proposal({ phase: phase("p9002"), at: "2026-09-15T10:05:00Z" }),
      filing: filing({ at: "2026-09-15T10:04:00Z" }),
    });

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-proposal")).toHaveTextContent("goal of p9002");
    expect(screen.queryByTestId("dialog-filed")).not.toBeInTheDocument();
  });

  it("SpecDialog_AReplyThatWasOnlyADraft_AddsNoEmptyTurn", async () => {
    reloadedWith({
      transcript: [
        { role: "user", text: "draft it", at: "2026-09-15T10:01:00Z" },
        { role: "assistant", text: "", at: "2026-09-15T10:02:00Z" },
      ],
    });
    await renderSurface();
    await screen.findByTestId("dialog-turn-user");

    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "", at: new Date().toISOString(),
    }));

    expect(screen.queryByTestId("dialog-turn-agent")).not.toBeInTheDocument();
  });

  // 2026-09-17-c7aed: the draft is not prose in the exchange; the turn that proposed it
  // carries a card. Live every proposing turn does; after a reload the latest does.
  it("SpecDialog_AProposingTurn_ShowsACard_LiveAndAfterAReload", async () => {
    await renderSurface();
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "", at: new Date().toISOString(),
    }));
    act(() => proposals.emit(proposal({
      kind: "epic", phase: null, parent: phase("p9000"), children: [phase("p9000a"), phase("p9000b")],
    })));

    const live = await screen.findByTestId("dialog-turn-card");
    const card = within(live).getByTestId("dialog-card");
    expect(card).toHaveAttribute("data-kind", "epic");
    expect(card).toHaveTextContent("goal of p9000");
    expect(card).toHaveTextContent("p9000a");
    expect(card).toHaveTextContent("p9000b");
    expect(card).not.toHaveTextContent("step of p9000a");

    cleanup();
    __forgetDialogIdForTests();
    reloadedWith({
      transcript: [
        { role: "user", text: "update every dependency", at: "2026-09-15T10:01:00Z" },
        { role: "assistant", text: "Server first.", at: "2026-09-15T10:02:00Z" },
        { role: "user", text: "then draft it", at: "2026-09-15T10:03:00Z" },
        { role: "assistant", text: "", at: "2026-09-15T10:04:00Z" },
      ],
      proposal: proposal({ phase: phase("p9001") }),
      proposalTurn: 3,
    });
    render(<SpecDialogSurface />);

    const reloaded = await screen.findByTestId("dialog-turn-card");
    expect(within(reloaded).getByTestId("dialog-card")).toHaveTextContent("goal of p9001");
    expect(within(screen.getByTestId("dialog-turn-agent")).queryByTestId("dialog-card")).toBeNull();
  });

  it("SpecDialog_TheCard_MovesThePaneToTheProposal", async () => {
    await renderSurface();
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "here it is", at: new Date().toISOString(),
    }));
    act(() => proposals.emit(proposal()));
    act(() => filings.emit(filing()));
    await screen.findByTestId("dialog-filed");

    fireEvent.click(within(screen.getByTestId("dialog-turn-agent")).getByTestId("dialog-card-inspect"));

    expect(await screen.findByTestId("dialog-proposal")).toHaveTextContent("goal of p9001");
    expect(screen.queryByTestId("dialog-filed")).not.toBeInTheDocument();
    expect(screen.getByTestId("dialog-tab-proposal")).toHaveAttribute("aria-selected", "true");
  });

  // 2026-09-18-2f8b RETIRES 2026-09-17-c7aed's "and no duration, because nothing measures
  // one". The turn's start instant is kept now, so the line states the seconds and the step
  // count. What this test still holds is the half that did not change: the reading lines name
  // each repository as it opens.
  it("SpecDialog_ARunningTurn_NamesEachRepositoryItOpensAndHowLongItHasBeen", async () => {
    await renderSurface();
    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    const working = await screen.findByTestId("dialog-working");

    act(() => readings.emit({
      dialogId: heldDialogId(), repo: "repo-a@v2", state: "opening", at: new Date().toISOString(),
    }));

    expect(working).toHaveTextContent("Opening the repositories it needs");
    expect(within(working).getByTestId("dialog-reading")).toHaveTextContent("repo-a@v2");
    expect(within(working).getByTestId("dialog-working-pulse")).toHaveTextContent("0s · 0 steps");
  });

  // 2026-09-22-b3d7: this line is the one place the card counts what the button files, and the
  // button now files ONE ticket whatever the slice count. Counting linked slice records beside it
  // described the shape the filer stopped producing.
  it("ApprovalCard_ACutProposal_SaysOneTicketAndCountsNoRecords", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal({
      kind: "epic", phase: null, parent: phase("p9000"),
      children: [phase("p9000a"), phase("p9000b"), phase("p9000c")],
    })));

    act(() => questions.emit(question({ text: "Proposed outcome: **epic** p9000" })));

    const summary = await screen.findByTestId("dialog-approval-summary");
    expect(summary).toHaveTextContent(/^File this epic\? One work ticket carrying 3 slices\.$/);
    expect(summary.textContent ?? "").not.toMatch(/record/i);
  });

  it("SpecDialog_TheApprovalSurface_StatesOnlyWhatTheProposalCarries", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal({
      kind: "epic", phase: null, parent: phase("p9000"),
      children: [phase("p9000a"), phase("p9000b", { requires: ["p9000a"] })],
    })));

    act(() => questions.emit(question({ text: "Proposed outcome: **epic** p9000" })));

    const surface = await screen.findByTestId("dialog-question");
    expect(within(surface).getByTestId("dialog-approval-summary"))
      .toHaveTextContent(/^File this epic\? One work ticket carrying 2 slices\.$/);
    expect(surface).toHaveTextContent("Proposed outcome: epic p9000");
    expect(screen.getByTestId("dialog-answer-approve")).toHaveTextContent("Approve & file");

    act(() => questions.emit(question({ kind: "confirmation", text: "Keep the scope?" })));

    expect(await screen.findByTestId("dialog-answer-yes")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-approval-summary")).toBeNull();
  });

  // 2026-09-17-042ed: the review of the proposal is read where the approval is given, with the
  // evidence line the framework minted for the look it rests on.
  it("SpecDialog_TheApprovalSurface_ListsEachFindingWithItsEvidence", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal({
      findings: [
        {
          phaseId: "p9001", problem: "false premise", why: "the endpoint already exists",
          quote: null, evidence: "[P2] repo-a: the proposal review ran 'read src/Api.cs' exited 0",
        },
        {
          phaseId: "p9001", problem: "contradiction", why: "it also forbids touching source",
          quote: "done of p9001", evidence: null,
        },
      ],
    })));

    act(() => questions.emit(question({ text: "Proposed outcome: **one phase** p9001" })));

    const surface = await screen.findByTestId("dialog-question");
    const findings = within(surface).getAllByTestId("dialog-proposal-finding");
    expect(findings).toHaveLength(2);
    expect(findings[0]).toHaveTextContent("p9001 — false premise: the endpoint already exists");
    expect(within(surface).getByTestId("dialog-finding-evidence"))
      .toHaveTextContent("[P2] repo-a: the proposal review ran 'read src/Api.cs' exited 0");
    expect(within(surface).getByTestId("dialog-finding-quote")).toHaveTextContent("done of p9001");
  });

  it("SpecDialog_ACleanReview_ShowsNoFindings", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal()));

    act(() => questions.emit(question({ text: "Proposed outcome: **one phase** p9001" })));

    await screen.findByTestId("dialog-question");
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
  });

  // A proposal stored before the review shipped, and every push from a server a version behind,
  // carries no findings field at all. Reading .length off it would throw and blank the whole
  // approval card — the surface on which the operator decides.
  it("SpecDialog_AProposalWithoutAFindingsField_StillRenders", async () => {
    await renderSurface();
    const older = proposal();
    delete (older as Partial<SpecDialogProposalPush>).findings;
    act(() => proposals.emit(older));

    act(() => questions.emit(question({ text: "Proposed outcome: **one phase** p9001" })));

    const surface = await screen.findByTestId("dialog-question");
    expect(surface).toHaveTextContent("Proposed outcome");
    expect(screen.queryByTestId("dialog-proposal-findings")).toBeNull();
  });

  // 2026-09-17-042ek: the server sends this page an approval with NO text — the card already
  // carries the counted summary, the findings and the two buttons, and a server sentence
  // saying any of it would show it twice.
  it("SpecDialog_AnApprovalWithNoServerText_StillSaysWhatIsBeingApproved", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal({
      kind: "epic", phase: null, parent: phase("p9000"),
      children: [phase("p9000a"), phase("p9000b")],
      findings: [{
        phaseId: "p9000a", problem: "false premise", why: "the endpoint already exists",
        quote: null, evidence: "[P2] repo-a: read src/Api.cs exited 0",
      }],
    })));

    act(() => questions.emit(question({ text: "" })));

    const surface = await screen.findByTestId("dialog-question");
    expect(within(surface).getByTestId("dialog-approval-summary"))
      .toHaveTextContent(/^File this epic\? One work ticket carrying 2 slices\.$/);
    expect(within(surface).getAllByTestId("dialog-proposal-finding")).toHaveLength(1);
    expect(screen.getByTestId("dialog-answer-approve")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-approval-unsummarised")).toBeNull();
    expect(within(surface).queryByTestId("markdown")).toBeNull();
  });

  // The proposal reaches the page as a live push, and the saved copy is dropped when its write
  // fails. A reload can therefore land on an approval with nothing to summarise — and with the
  // server text now empty, an unexplained pair of buttons unless the card says so itself.
  //
  // Found by review: the draft is not "in the conversation above" in this case. The shown
  // transcript strips every draft block and the seed drops the agent entry that leaves empty,
  // so the page holds nothing about it at all — and telling the operator to look up the page
  // for something that was never rendered is worse than saying nothing.
  it("SpecDialog_AnApprovalWithoutASavedProposal_SaysNothingWasSavedAndPointsAtReject", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      question: {
        dialogId: "any", questionId: "q-9", kind: "approval", text: "", choices: [],
        at: "2026-09-15T10:01:00Z",
        expiresAt: new Date(Date.now() + 600_000).toISOString(),
      },
    }));

    render(<SpecDialogSurface />);

    const surface = await screen.findByTestId("dialog-question");
    const line = within(surface).getByTestId("dialog-approval-unsummarised");
    expect(line).toHaveTextContent("Nothing was saved about this proposal");
    expect(line).toHaveTextContent("Reject it and ask for a fresh one");
    expect(line).not.toHaveTextContent("above");
    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-approval-summary")).toBeNull();
  });

  // The other way the summary goes missing: a proposal DID arrive, of a kind this build cannot
  // count. Its findings render from it and the pane has it in full, so the two cases must not
  // share a sentence — one says the page has nothing, and here the page has almost everything.
  it("SpecDialog_AnApprovalWhoseProposalCannotBeCounted_SaysSoWithoutClaimingItIsLost", async () => {
    await renderSurface();
    act(() => proposals.emit(proposal({
      kind: "phase", phase: null, bug: null, parent: null, children: [],
      findings: [{
        phaseId: "p9001", problem: "contradiction", why: "it also forbids touching source",
        quote: "done of p9001", evidence: null,
      }],
    })));

    act(() => questions.emit(question({ text: "" })));

    const surface = await screen.findByTestId("dialog-question");
    const line = within(surface).getByTestId("dialog-approval-unsummarised");
    expect(line).toHaveTextContent("cannot count what it would file");
    expect(line).not.toHaveTextContent("Nothing was saved");
    expect(within(surface).getAllByTestId("dialog-proposal-finding")).toHaveLength(1);
  });

  // An expired approval has no summary, no findings and — since the server text went empty —
  // nothing at all but the eyebrow. The line beside its (absent) buttons promised a revision
  // the confirmer had already stopped waiting to make.
  it("SpecDialog_AnExpiredQuestion_PromisesATurnNotARevision", async () => {
    await renderSurface();

    act(() => questions.emit(question({
      text: "", expiresAt: new Date(Date.now() - 1000).toISOString(),
    })));

    const surface = await screen.findByTestId("dialog-question");
    expect(surface).toHaveTextContent("no longer waiting");
    expect(surface).toHaveTextContent("anything you write below starts a new turn");
    expect(surface).not.toHaveTextContent("the proposal is revised with it");
    expect(screen.queryByTestId("dialog-approval-unsummarised")).toBeNull();
  });

  it("SpecDialog_ATabWithNothingToShow_IsNotOffered", async () => {
    await renderSurface();

    expect(screen.getByTestId("dialog-tab-scope")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-tab-proposal")).toBeNull();
    expect(screen.queryByTestId("dialog-tab-filed")).toBeNull();

    act(() => proposals.emit(proposal()));

    expect(await screen.findByTestId("dialog-tab-proposal")).toHaveAttribute("aria-selected", "true");
    expect(screen.queryByTestId("dialog-tab-filed")).toBeNull();
  });

  // Found by review: the server lets a proposal go on a rejection and on a timed-out approval,
  // and a reload showed it gone while the live page kept its tab and an inspectable card. The
  // page sees only the notice and the read after it; the two notices are told apart by nothing
  // but their words, so each is proven the same way.
  async function proposalLetGoBy(notice: string) {
    await renderSurface();
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: "", at: new Date().toISOString() }));
    act(() => proposals.emit(proposal()));
    act(() => questions.emit(question()));
    await screen.findByTestId("dialog-card-inspect");
    expect(screen.getByTestId("dialog-tab-proposal")).toBeInTheDocument();

    reloadedWith({ proposal: null, proposalTurn: null });
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: notice, at: new Date().toISOString() }));

    await waitFor(() => expect(screen.queryByTestId("dialog-tab-proposal")).toBeNull());
    expect(screen.queryByTestId("dialog-card-inspect")).toBeNull();
    expect(screen.queryByTestId("dialog-turn-card")).toBeNull();

    cleanup();
    __forgetDialogIdForTests();
    render(<SpecDialogSurface />);
    await screen.findByTestId("dialog-transcript-empty");
    expect(screen.queryByTestId("dialog-tab-proposal")).toBeNull();
    expect(screen.queryByTestId("dialog-card-inspect")).toBeNull();
  }

  it("SpecDialog_ARejectedProposal_LeavesNoTabAndNoCard_LiveAsAfterAReload", async () => {
    await proposalLetGoBy("Rejected — nothing was filed.");
  });

  it("SpecDialog_ATimedOutApproval_LeavesNoTabAndNoCard_LiveAsAfterAReload", async () => {
    await proposalLetGoBy("Nobody answered in time — nothing was filed.");
  });

  // Found by review: the first read could come back after a live proposal and put the one it
  // had read — or none — back over it, taking the card with it and leaving the approval
  // summary counting the wrong draft.
  it("SpecDialog_AProposalPushedWhileTheFirstReadIsOut_SurvivesTheRead", async () => {
    let answer: (value: SpecDialogView) => void = () => {};
    fetchSpecDialog.mockImplementationOnce(() => new Promise<SpecDialogView>((resolve) => { answer = resolve; }));
    render(<SpecDialogSurface />);
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    const dialogId = heldDialogId();

    act(() => proposals.emit(proposal({
      dialogId, kind: "epic", phase: null, parent: phase("p9000"),
      children: [phase("p9000a"), phase("p9000b")],
    })));
    act(() => questions.emit(question({ dialogId })));
    const base = view({ dialogId });
    await act(async () => answer({
      ...base,
      session: {
        ...base.session!,
        transcript: [{ role: "user", text: "draft it", at: "2026-09-15T10:01:00Z" }],
        proposal: proposal({ dialogId, phase: phase("p8000") }),
        proposalTurn: null,
      },
    }));

    expect(await screen.findByTestId("dialog-turn-user")).toHaveTextContent("draft it");
    expect(within(screen.getByTestId("dialog-turn-card")).getByTestId("dialog-card")).toHaveAttribute("data-kind", "epic");
    expect(screen.getByTestId("dialog-proposal")).toHaveTextContent("goal of p9000");
    expect(screen.getByTestId("dialog-approval-summary"))
      .toHaveTextContent(/^File this epic\? One work ticket carrying 2 slices\.$/);
  });

  // The read that raced the push may still have caught the draft stored, stamped with a moment
  // of its own; the card is the live one, once.
  it("SpecDialog_AReadThatCaughtTheLiveProposalStored_ShowsItsCardOnce", async () => {
    let answer: (value: SpecDialogView) => void = () => {};
    fetchSpecDialog.mockImplementationOnce(() => new Promise<SpecDialogView>((resolve) => { answer = resolve; }));
    render(<SpecDialogSurface />);
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    const dialogId = heldDialogId();

    act(() => proposals.emit(proposal({ dialogId, at: "2026-09-15T10:03:00Z" })));
    const base = view({ dialogId });
    await act(async () => answer({
      ...base,
      session: {
        ...base.session!,
        transcript: [
          { role: "user", text: "draft it", at: "2026-09-15T10:01:00Z" },
          { role: "assistant", text: "", at: "2026-09-15T10:02:00Z" },
        ],
        proposal: proposal({ dialogId, at: "2026-09-15T10:02:00Z" }),
        proposalTurn: 1,
      },
    }));

    await screen.findByTestId("dialog-turn-user");
    expect(screen.getAllByTestId("dialog-card")).toHaveLength(1);
  });

  // Found by review: the draft-only flag outlived the conversation it was set in, so a card on
  // the next conversation took a turn of its own instead of the reply it followed.
  it("SpecDialog_ANewConversation_ForgetsThatTheLastReplyWasOnlyADraft", async () => {
    await renderSurface();
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: "", at: new Date().toISOString() }));
    const first = heldDialogId();
    reloadedWith({ transcript: [{ role: "assistant", text: "Server first.", at: "2026-09-15T10:02:00Z" }] });

    fireEvent.click(screen.getByTestId("dialog-new"));
    await waitFor(() => expect(heldDialogId()).not.toBe(first));
    await screen.findByTestId("dialog-turn-agent");
    act(() => proposals.emit(proposal()));

    expect(within(await screen.findByTestId("dialog-turn-agent")).getByTestId("dialog-card")).toBeInTheDocument();
    expect(screen.queryByTestId("dialog-turn-card")).toBeNull();
  });

  // Live every proposing turn keeps its card, and an earlier card shows its own draft, marked.
  it("SpecDialog_InspectingAnEarlierCard_ShowsItsDraftMarkedSuperseded", async () => {
    await renderSurface();
    reloadedWith({ proposal: proposal() });
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: "a first cut", at: new Date().toISOString() }));
    act(() => proposals.emit(proposal()));
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: "a second cut", at: new Date().toISOString() }));
    act(() => proposals.emit(proposal({ phase: phase("p9002") })));

    await waitFor(() => expect(screen.getAllByTestId("dialog-card-inspect")).toHaveLength(2));
    expect(screen.getByTestId("dialog-pane")).toHaveTextContent("not filed yet");

    fireEvent.click(screen.getAllByTestId("dialog-card-inspect")[0]);

    const pane = screen.getByTestId("dialog-pane");
    expect(within(pane).getByTestId("dialog-proposal")).toHaveTextContent("goal of p9001");
    expect(pane).toHaveTextContent("superseded");
  });

  // Found by review: the speaker was an aria-label on a div with no role, every card's link read
  // "Inspect →", and the pane's tabs pointed at no panel.
  it("SpecDialog_SpeakersCardsAndTabs_AreNamedForAssistiveTechnology", async () => {
    await renderSurface();
    act(() => messages.emit({ dialogId: heldDialogId(), title: "Spec dialog", text: "here it is", at: new Date().toISOString() }));
    act(() => proposals.emit(proposal()));

    const turn = await screen.findByTestId("dialog-turn-agent");
    expect(turn.querySelector(".sr-only")).toHaveTextContent("agent-smith:");
    expect(turn.querySelector("[aria-hidden='true']")).toHaveTextContent("AS");
    expect(screen.getByRole("button", { name: "Inspect the phase proposal: goal of p9001" })).toBeInTheDocument();
    const selected = screen.getByRole("tab", { selected: true });
    const panel = screen.getByRole("tabpanel");
    expect(selected).toHaveAttribute("aria-controls", panel.id);
    expect(panel).toHaveAttribute("aria-labelledby", selected.id);
  });

  // 2026-09-17-042ej: THE CONVERSATION FOLLOWS WHAT IT FILED. The filing push names the ticket;
  // the read beneath it says which phase the run is on, what it opened, and what the review left.
  it("SpecDialog_TheFiledTab_ShowsTheRunARowPerPhaseItsPullRequestsAndFindings", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const pane = await screen.findByTestId("dialog-filed");
    await waitFor(() =>
      expect(screen.getByTestId("dialog-filed-run-2026-09-17T09-00-00-0001")).toBeInTheDocument());
    expect(pane.querySelector("a[href='/jobs/2026-09-17T09-00-00-0001']")).toBeInTheDocument();
    expect(pane.querySelector("a[href='https://git/pr/3']")).toBeInTheDocument();
    expect(screen.getByTestId("dialog-filed-phase-p9001a")).toHaveTextContent("Make the thing exist");
    expect(screen.getByTestId("dialog-filed-findings-p9001a")).toHaveTextContent("src/A.cs:4");
    // The whole point of the report being an object: a review nobody took is not a clean one.
    expect(screen.getByTestId("dialog-filed-unreviewed-p9001b"))
      .toHaveTextContent("not reviewed: the run's configured cost cap is exhausted");
    expect(screen.queryByTestId("dialog-filed-reviewed-p9001b")).toBeNull();
  });

  // 2026-09-18-e63d: the same class of text one tab over, and the reason this phase does not
  // stop at marks. The repository a pull request is against is a flex item of the mark row, and
  // the project the run is in sits in the head line — both named by somebody else, both inside
  // a card that clips. A phase id one row below is NOT given text: this page mints it, and it
  // keeps the plain field value.
  it("SpecDialog_TheFiledPanesGivenText_AsksToWrap", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    const where = run.querySelector(".ec-sub") as HTMLElement;
    expect(where.className.split(/\s+/)).toContain("given");
    expect(where).toHaveTextContent("in sample");

    const repositories = [...run.querySelectorAll("ul.ec-marks .fv")];
    expect(repositories.map((el) => el.textContent?.replace(" \u2197", ""))).toEqual(["api", "docs", "infra"]);
    for (const repository of repositories) {
      expect(repository.className.split(/\s+/), repository.textContent ?? "").toContain("given");
    }

    const phaseId = screen.getByTestId("dialog-filed-phase-p9001a").querySelector(".fv") as HTMLElement;
    expect(phaseId.className.split(/\s+/)).not.toContain("given");
  });

  // The third and fourth states of the review, and the one finding that says the branch still
  // carries the code it is about — none of which the run/phase test above touches.
  it("SpecDialog_TheFiledTab_TellsAnUnreviewedPhaseFromARevertedFinding", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    await waitFor(() =>
      expect(screen.getByTestId("dialog-filed-noreview-p9001c")).toBeInTheDocument());
    expect(screen.getByTestId("dialog-filed-noreview-p9001c")).toHaveTextContent("not reviewed yet");
    // 2026-09-17-042ef: keyed per FINDING, because a phase may revert more than one.
    expect(screen.getByTestId("dialog-filed-reverted-p9001a-api/src/A.cs:4"))
      .toHaveTextContent("the fix pass was reverted: the suite stayed red");
    expect(screen.getByTestId("dialog-filed-reverted-p9001a-api/src/B.cs:9"))
      .toHaveTextContent("the fix pass was reverted: the build stayed red");
  });

  // A failed phase shows the verdict its row carries and NOTHING else: the only producer of a
  // failed row is the phase's own verification, so an amendment would change nothing.
  it("SpecDialog_APhaseFailedByARedBuild_ShowsItsVerdictAndNoAmendmentInstruction", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(stoppedPhase("failed", "dotnet test exited 1"));

    act(() => filings.emit(filing()));

    const verdict = await screen.findByTestId("dialog-filed-verdict-p9001a");
    expect(verdict).toHaveTextContent("dotnet test exited 1");
    expect(screen.queryByTestId("dialog-filed-amend-p9001a")).toBeNull();
    expect(screen.getByTestId("dialog-filed")).not.toHaveTextContent("approve the set again");
  });

  // Its twin, and the pair is the whole point: the two statuses ask opposite things of the
  // operator, so a page that collapsed them would send one of them to the wrong repair.
  it("SpecDialog_APhaseHandedBackOnAPremise_AsksForTheAmendment", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(
      stoppedPhase("handed_back", "the premise 'api has no cache' is false (P3)"));

    act(() => filings.emit(filing()));

    expect(await screen.findByTestId("dialog-filed-amend-p9001a"))
      .toHaveTextContent("approve the set again in this conversation");
    expect(screen.getByTestId("dialog-filed-verdict-p9001a"))
      .toHaveTextContent("the premise 'api has no cache' is false");
  });

  // 2026-09-17-042ef, all six found by looking at the rendered page rather than at the markup.
  // A state rendered as the column value the projection writes — "in_progress", "not_started" —
  // inline in the sentence around it, so it could not be told from prose. Every state is the
  // Projects page's mark now, and the words are English.
  it("SpecDialog_APhaseAndItsRun_ShowTheirStateAsAWrittenMarkNotAColumnValue", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const running = await screen.findByTestId("dialog-filed-phase-p9001b");
    expect(running.querySelector(".ec-mark")).toHaveTextContent("running");
    expect(running.textContent).not.toContain("in_progress");
    const waiting = screen.getByTestId("dialog-filed-phase-p9001c");
    expect(waiting.querySelector(".ec-mark")).toHaveTextContent("not started");
    expect(waiting.textContent).not.toContain("not_started");
    const run = screen.getByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    expect(run.querySelector(".ec-mark")).toHaveTextContent("running");
  });

  // The two stopped states ask opposite things of the operator, so they do not share a mark
  // any more than 042ej let them share a sentence.
  it("SpecDialog_APhaseStoppedRed_AndOneHandedBack_CarryDifferentMarks", async () => {
    const markOf = async (status: string) => {
      await renderSurface();
      fetchFiledWork.mockResolvedValue(stoppedPhase(status, "why it stopped"));
      act(() => filings.emit(filing()));
      const row = await screen.findByTestId("dialog-filed-phase-p9001a");
      return row.querySelector(".ec-mark") as HTMLElement;
    };

    const red = await markOf("failed");
    expect(red).toHaveClass("bad");
    expect(red).toHaveTextContent("failed");
    cleanup();
    __forgetDialogIdForTests();
    const premise = await markOf("handed_back");
    expect(premise).toHaveClass("warn");
    expect(premise).toHaveTextContent("handed back");
  });

  // Two findings, two addresses and a revert note arrived as one unbroken block. Each finding
  // is a row, and the address it rests on is set above the prose the way the reference sets a
  // mono name above its description.
  it("SpecDialog_TwoFindingsUnderOnePhase_AreTwoRowsEachWithItsAddressSetApart", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const findings = await screen.findByTestId("dialog-filed-findings-p9001a");
    const rows = findings.querySelectorAll("li.d-finding");
    expect(rows).toHaveLength(2);
    expect(rows[0].querySelector(".fv")).toHaveTextContent("api/src/A.cs:4");
    expect(rows[1].querySelector(".fv")).toHaveTextContent("api/src/B.cs:9");
    expect(rows[0]).toHaveTextContent("the catch body logs nothing");
    expect(rows[1]).toHaveTextContent("the retry count is a literal");
  });

  // 2026-09-17-042eh exists to keep "nobody looked" apart from "looked and found nothing", and
  // on screen the separation was a word. The two states where nobody looked are the two that
  // carry an alarm mark; a clean review and a phase that has not reached its review are calm.
  it("SpecDialog_AReviewNobodyTook_IsMarkedApartFromACleanOne", async () => {
    const markOf = async (review: FiledWorkReview | null, testId: string) => {
      await renderSurface();
      fetchFiledWork.mockResolvedValue(reviewedBy(review));
      act(() => filings.emit(filing()));
      const row = await screen.findByTestId(`${testId}-p9001a`);
      return row.querySelector(".ec-mark") as HTMLElement;
    };
    const again = () => { cleanup(); __forgetDialogIdForTests(); };

    const skipped = await markOf(
      { reviewed: false, why: "the cost cap is exhausted", findings: [], unreadable: false },
      "dialog-filed-unreviewed");
    expect(skipped).toHaveClass("warn");
    expect(skipped.parentElement).toHaveTextContent("not reviewed: the cost cap is exhausted");

    again();
    expect(await markOf(
      { reviewed: false, why: "the row could not be read", findings: [], unreadable: true },
      "dialog-filed-unreadable")).toHaveClass("bad");

    again();
    const clean = await markOf(
      { reviewed: true, why: null, findings: [], unreadable: false }, "dialog-filed-reviewed");
    expect(clean.className).toBe("ec-mark");

    // The third gap. A phase that STOPPED and wrote no review row has a hole where its evidence
    // should be, and read in the same calm grey as "reviewed, nothing found" beside it. A phase
    // still working has simply not got there, and stays calm.
    again();
    const missing = await markOf(null, "dialog-filed-noreview");
    expect(missing).toHaveClass("warn");
    expect(missing).toHaveTextContent("no review was recorded");
  });

  it("SpecDialog_APhaseStillRunning_SaysItsReviewIsNotReachedWithoutRaisingAnAlarm", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const notYet = await screen.findByTestId("dialog-filed-noreview-p9001c");
    expect(notYet.querySelector(".ec-mark")?.className).toBe("ec-mark");
    expect(notYet).toHaveTextContent("not reviewed yet");
  });

  // "Filed" stood three times in one corner: the selected tab, the eyebrow beside it and the
  // panel's own heading. The tab is the name; the eyebrow says only what the name cannot.
  it("SpecDialog_TheFiledPane_NamesItselfOnceAndSaysNothingElseWhenNothingFailed", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const pane = await screen.findByTestId("dialog-pane");
    expect(screen.getByTestId("dialog-tab-filed")).toHaveTextContent("Filed");
    expect(within(pane).queryByRole("heading")).toBeNull();
    expect(pane.querySelector(".d-head .fl")).toHaveTextContent("");
  });

  // Dropping the panel's heading was right for a filing that worked and wrong for one that did
  // not: it left the failure as 9.5px of the lightest ink on the page. The label keeps its size
  // and takes the colour the studio gives a broken thing.
  it("SpecDialog_AFailedFiling_SaysSoBesideTheTabAndInTheToneOfAFailure", async () => {
    await renderSurface();

    act(() => filings.emit(filing({ error: "the tracker refused" })));

    const pane = await screen.findByTestId("dialog-pane");
    const eyebrow = pane.querySelector(".d-head .fl");
    expect(eyebrow).toHaveTextContent("filing failed");
    expect(eyebrow?.className.split(/\s+/)).toContain("bad");
    // And the sentence under it went the other way when the heading left: it dropped to the
    // panel's quietest class. A failure is not a sub-line.
    const lead = screen.getByTestId("dialog-filed").querySelector("p");
    expect(lead).toHaveTextContent("These tickets were created before it stopped");
    expect(lead?.className.split(/\s+/)).toContain("text-ink");
    expect(lead?.className.split(/\s+/)).not.toContain("ec-sub");
  });

  // The Scope tab said itself twice — as the tab, and as an eyebrow beside it — over a panel
  // whose own first sentence says the same thing a third time. The eyebrow carries STATE, and
  // a list of what a conversation may read is not one.
  it("SpecDialog_TheScopePane_SaysWhatItIsOnceBesideItsTab", async () => {
    await renderSurface();

    const pane = await screen.findByTestId("dialog-pane");
    fireEvent.click(screen.getByTestId("dialog-tab-scope"));

    expect(screen.getByTestId("dialog-tab-scope")).toHaveTextContent("Scope");
    expect(pane.querySelector(".d-head .fl")).toHaveTextContent("");
    expect(screen.getByTestId("dialog-scope")).toHaveTextContent("What this conversation may read.");
  });

  // The run line read "…in sample · $2.50" and then a bare repository name, with nothing on it
  // to say the second was a pull request rather than another repository the run had touched.
  it("SpecDialog_ARunsPullRequests_SayThatIsWhatTheyAre", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    expect(run).toHaveTextContent("pull request");
    expect(run.querySelector("a[href='https://git/pr/3']")).toHaveTextContent("api");
    expect(run).toHaveTextContent("opened");
  });

  // Its state was the column value, underscore and all, and a pull request that could not be
  // opened read in the same grey as one that was — while a skipped review one panel over was
  // amber. The reason the row carries for a failure was never shown at all.
  it("SpecDialog_APullRequestState_IsEnglishAndAFailureAlarmsAndSaysWhy", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    expect(run).toHaveTextContent("no changes");
    expect(run.textContent).not.toContain("no_changes");
    const alarm = [...run.querySelectorAll(".ec-mark.bad")];
    expect(alarm).toHaveLength(1);
    expect(alarm[0]).toHaveTextContent("could not be opened");
    expect(run).toHaveTextContent("the branch was rejected by the remote");
  });

  // 2026-09-20-c206: the row opened with a timestamp, under a ticket line that opens with its
  // state. The status leads now. jsdom loads no stylesheet, so the weight is read off the class
  // list — the idiom this suite already uses for a token's rendering.
  it("SpecDialog_ARunRow_RendersItsStatusBeforeItsIdentifier", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    const status = run.querySelector(".ec-mark") as HTMLElement;
    const identifier = run.querySelector("a[href='/jobs/2026-09-17T09-00-00-0001']") as HTMLElement;
    expect(status).toHaveTextContent("running");
    expect(
      status.compareDocumentPosition(identifier) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  // Second place does not stop a heavier token reading first, so the weight goes with the move —
  // and nothing else about the link does.
  it("SpecDialog_TheRunIdentifier_CarriesNoFontSemibold", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    const identifier = run.querySelector("a[href='/jobs/2026-09-17T09-00-00-0001']") as HTMLElement;
    const classes = identifier.className.split(/\s+/);
    expect(classes).not.toContain("font-semibold");
    expect(classes).toContain("d-link");
    expect(classes).toContain("font-mono");
    expect(classes).toContain("dsh-label");
    expect(identifier).toHaveTextContent("2026-09-17T09-00-00-0001");
  });

  // The run block holds six sub-texts and an assertion elsewhere takes the first of them, so
  // which token is first of its kind is load-bearing and not a detail of this row.
  it("SpecDialog_TheProjectAndCost_AreStillTheFirstSubTextInTheRunBlock", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    expect(run.querySelector(".ec-sub")).toHaveTextContent("in sample \u00b7 $2.50");
  });

  // The move is a reorder and a weight, never a removal.
  it("SpecDialog_AReorderedRunRow_StillCarriesEveryFactItCarried", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing()));

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    expect(run).toHaveTextContent("2026-09-17T09-00-00-0001");
    expect(run).toHaveTextContent("running");
    expect(run).toHaveTextContent("in sample");
    expect(run).toHaveTextContent("$2.50");
    expect(run.querySelector("a[href='https://git/pr/3']")).toHaveTextContent("api");
  });

  // The rule this phase borrows is written one component above, in a comment: the state first,
  // then why. Both rows lead with their state now — and only the ticket's is weighted, because
  // weight belongs on a state and not on an identifier.
  it("SpecDialog_TheRunRowAndTheTicketLineAboveIt_LeadWithTheirStateAndOnlyTheTicketsIsWeighted", async () => {
    await renderSurface();
    fetchFiledWork.mockResolvedValue(filedWork());

    act(() => filings.emit(filing({
      filed: [
        {
          reference: "https://tracker/7",
          title: "p9001: the phase",
          ticketId: "7",
          project: "sample",
          start: { state: "Started", reason: "it already triggers" },
        },
      ],
    })));

    const ticketState = (await screen.findByTestId("dialog-filed-start-https://tracker/7"))
      .firstElementChild as HTMLElement;
    expect(ticketState).toHaveTextContent("started");
    expect(ticketState.className.split(/\s+/)).toContain("font-semibold");

    const run = await screen.findByTestId("dialog-filed-run-2026-09-17T09-00-00-0001");
    const status = run.querySelector(".ec-mark") as HTMLElement;
    const identifier = run.querySelector("a[href='/jobs/2026-09-17T09-00-00-0001']") as HTMLElement;
    expect(status).toHaveTextContent("running");
    expect(status.className.split(/\s+/)).not.toContain("font-semibold");
    expect(identifier.className.split(/\s+/)).not.toContain("font-semibold");
    expect(
      status.compareDocumentPosition(identifier) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
  });

  it("SpecDialog_AReconnect_RewatchesAndRereadsTheFiledWork", async () => {
    await renderSurface();
    await waitFor(() => expect(fetchFiledWork).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(watchFiledWork).toHaveBeenCalledTimes(1));

    act(() => connectionState.emit(HubConnectionState.Connected));

    await waitFor(() => expect(fetchFiledWork).toHaveBeenCalledTimes(2));
    expect(watchFiledWork).toHaveBeenCalledTimes(2);
  });

  // The watch is what the nudge is addressed by, and the SERVER reads the ticket ids off the
  // latest filing - so a watch registered before the filing follows nothing.
  it("SpecDialog_TheWatch_IsIssuedOnConnectAndAgainAfterAFiling", async () => {
    await renderSurface();
    await waitFor(() => expect(watchFiledWork).toHaveBeenCalledTimes(1));
    expect(watchFiledWork).toHaveBeenCalledWith(heldDialogId());

    act(() => filings.emit(filing()));

    await waitFor(() => expect(watchFiledWork).toHaveBeenCalledTimes(2));
    expect(watchFiledWork).toHaveBeenLastCalledWith(heldDialogId());
  });

  it("SpecDialog_ANudge_RefetchesTheFiledWorkOncePerWindow", async () => {
    await renderSurface();
    await waitFor(() => expect(fetchFiledWork).toHaveBeenCalledTimes(1));

    act(() => {
      filedWorkChanged.emit(undefined);
      filedWorkChanged.emit(undefined);
      filedWorkChanged.emit(undefined);
    });

    await waitFor(() => expect(fetchFiledWork).toHaveBeenCalledTimes(2));
    await new Promise((settle) => setTimeout(settle, 400));
    expect(fetchFiledWork).toHaveBeenCalledTimes(2);
  });

  it("SpecDialog_AReloadWithoutANudge_ShowsTheSameRows", async () => {
    reloadedWith({ filing: filing({ at: "2026-09-15T10:04:00Z" }) });
    fetchFiledWork.mockImplementation(async (dialogId: string) => ({
      ...filedWork(),
      dialogId,
    }));

    render(<SpecDialogSurface />);

    expect(await screen.findByTestId("dialog-filed-phase-p9001a"))
      .toHaveTextContent("Make the thing exist");
    expect(screen.getByTestId("dialog-filed-run-2026-09-17T09-00-00-0001")).toBeInTheDocument();
  });

  it("SpecDialog_TheEmptyTranscript_SaysTheConversationFollowsWhatItFiles", async () => {
    fetchSpecDialog.mockResolvedValue(view({ session: null }));

    render(<SpecDialogSurface />);

    const empty = await screen.findByTestId("dialog-transcript-empty");
    expect(empty).toHaveTextContent("Filing is not where this ends");
    expect(empty).toHaveTextContent("follows the work it filed");
  });

  it("SpecDialog_APushForAnotherDialog_ChangesNothing", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal({ dialogId: "someone-elses-dialog" })));

    expect(screen.queryByTestId("dialog-proposal")).not.toBeInTheDocument();
    expect(screen.getByTestId("dialog-scope")).toBeInTheDocument();
  });

  // 2026-09-20-3af8: an operator shows the design partner what they are looking at. The image is
  // uploaded the moment it is picked — not held until Send — and the read that follows is what
  // puts it in the transcript, which is also what makes it survive a reload.
  it("SpecDialog_TheComposer_TakesAnImageAndTheTranscriptShowsIt", async () => {
    await renderSurface();
    fetchSpecDialog.mockResolvedValue(view({
      session: {
        ...view().session!,
        transcript: [turn("what is wrong with this")],
        images: [{ id: 7, mediaType: "image/png", at: "2026-09-15T09:30:00Z" }],
      },
    }));

    fireEvent.change(screen.getByTestId("dialog-composer-image"), {
      target: { files: [new File(["png-bytes"], "shot.png", { type: "image/png" })] },
    });

    await waitFor(() => expect(uploadSpecDialogImage).toHaveBeenCalled());
    const [dialogId, project, file] = uploadSpecDialogImage.mock.calls[0];
    expect(dialogId).toBe(heldDialogId());
    expect(project).toBe("sample");
    expect(file.name).toBe("shot.png");
    const shown = await screen.findByTestId("dialog-image-7");
    expect(shown).toHaveAttribute("src", "/api/spec-dialog/images/7");
  });

  // Nothing ties an image to a turn: the upload is a post of its own and the durable transcript
  // holds text. Both carry a moment, so the image sits after what was said before it.
  it("SpecDialog_AnAttachedImage_SitsAfterTheTurnItFollowed", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: {
        ...view().session!,
        transcript: [
          { role: "user", text: "before the screenshot", at: "2026-09-15T09:00:00Z" },
          { role: "user", text: "after the screenshot", at: "2026-09-15T10:00:00Z" },
        ],
        images: [{ id: 7, mediaType: "image/png", at: "2026-09-15T09:30:00Z" }],
      },
    }));
    await renderSurface();

    const transcript = await screen.findByTestId("dialog-transcript");
    const shown = within(transcript).getAllByTestId(/dialog-turn-(user|image)/);
    expect(shown.map((entry) => entry.getAttribute("data-testid"))).toEqual([
      "dialog-turn-user", "dialog-turn-image", "dialog-turn-user",
    ]);
  });
});

// 2026-09-20-4b0ae: INSPECT ACKNOWLEDGES ITSELF. An operator clicked Inspect on a proposal card
// and nothing appeared to happen. Every part of the chain was right — the focus is set, the tab
// is offered, the tab is selected — and none of it was visible, because the pane falls back to
// the proposal tab whenever nothing has been filed, so the commonest state is the pane ALREADY
// showing the proposal being inspected and the click renders the same document twice. What the
// act leaves behind is asserted here: a mark on the pane, a nearest scroll, and the focus.
describe("Inspecting reaches the pane", () => {
  /** A conversation that has proposed and filed: the pane is on Filed, so the inspect moves it. */
  async function proposedAndFiled() {
    await renderSurface();
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "here it is", at: new Date().toISOString(),
    }));
    act(() => proposals.emit(proposal()));
    act(() => filings.emit(filing()));
    await screen.findByTestId("dialog-filed");
  }

  /** A conversation that has proposed and filed NOTHING: the pane is already on that proposal. */
  async function proposedOnly() {
    await renderSurface();
    act(() => messages.emit({
      dialogId: heldDialogId(), title: "Spec dialog", text: "here it is", at: new Date().toISOString(),
    }));
    act(() => proposals.emit(proposal()));
    await screen.findByTestId("dialog-proposal");
  }

  function inspectTheCard() {
    fireEvent.click(within(screen.getByTestId("dialog-turn-agent")).getByTestId("dialog-card-inspect"));
  }

  it("SpecDialog_Inspecting_MarksThePane", async () => {
    await proposedAndFiled();
    expect(screen.getByTestId("dialog-pane")).not.toHaveAttribute("data-inspected");

    inspectTheCard();

    expect(screen.getByTestId("dialog-pane")).toHaveAttribute("data-inspected", "true");
  });

  // The case the whole phase exists for, and the one an earlier cut would have missed: the pane
  // is showing this very proposal already, so the click changes NOTHING inside it. The mark is
  // outside it — on the card's own element — which is why it still says the click landed.
  it("SpecDialog_InspectingTheProposalAlreadyShown_StillMarksThePane", async () => {
    await proposedOnly();
    const pane = screen.getByTestId("dialog-pane");
    expect(screen.getByTestId("dialog-tab-proposal")).toHaveAttribute("aria-selected", "true");
    const unchanged = pane.innerHTML;

    inspectTheCard();

    expect(pane.innerHTML, "the inspected proposal is the one already shown").toBe(unchanged);
    expect(pane).toHaveAttribute("data-inspected", "true");
  });

  // Nearest, and no behaviour option. The page's scroll container is the main region and the
  // pane's top is the top of the grid, so a default start-aligned scroll would throw the page
  // back to the top when Inspect is clicked at the bottom of a long transcript. The equality is
  // exact, so a behaviour added later fails here rather than being read as an improvement.
  it("SpecDialog_Inspecting_ScrollsThePaneIntoViewWithNearestAlignment", async () => {
    await proposedAndFiled();
    const scrolled = vi.spyOn(screen.getByTestId("dialog-pane"), "scrollIntoView");

    inspectTheCard();

    expect(scrolled).toHaveBeenCalledTimes(1);
    expect(scrolled).toHaveBeenCalledWith({ block: "nearest", inline: "nearest" });
  });

  // Where the act leaves the reader, and the half that works with no viewport at all.
  it("SpecDialog_Inspecting_MovesFocusToThePanePanel", async () => {
    await proposedAndFiled();
    expect(document.activeElement).not.toBe(screen.getByRole("tabpanel"));

    inspectTheCard();

    expect(document.activeElement).toBe(screen.getByRole("tabpanel"));
  });

  // The routes into the pane that are NOT an act the operator just performed. A push moves the
  // pane on its own, and yanking the viewport under someone who is reading is worse than the
  // defect this phase fixes — so neither the proposal nor the filing that follows it scrolls or
  // marks anything.
  it("SpecDialog_ANewProposal_MovesThePaneWithoutScrollingToIt", async () => {
    await renderSurface();
    const scrolled = vi.spyOn(Element.prototype, "scrollIntoView");
    try {
      act(() => messages.emit({
        dialogId: heldDialogId(), title: "Spec dialog", text: "here it is", at: new Date().toISOString(),
      }));
      act(() => proposals.emit(proposal()));

      await screen.findByTestId("dialog-proposal");
      expect(screen.getByTestId("dialog-tab-proposal")).toHaveAttribute("aria-selected", "true");
      expect(scrolled).not.toHaveBeenCalled();
      expect(screen.getByTestId("dialog-pane")).not.toHaveAttribute("data-inspected");

      act(() => filings.emit(filing()));

      await screen.findByTestId("dialog-filed");
      expect(scrolled).not.toHaveBeenCalled();
      expect(screen.getByTestId("dialog-pane")).not.toHaveAttribute("data-inspected");
    } finally {
      scrolled.mockRestore();
    }
  });

  // A tab inside the pane means the pane is already in view: the operator just clicked it.
  it("SpecDialog_ClickingAPaneTab_ScrollsNothing", async () => {
    await proposedAndFiled();
    const scrolled = vi.spyOn(Element.prototype, "scrollIntoView");
    try {
      fireEvent.click(screen.getByTestId("dialog-tab-proposal"));

      expect(await screen.findByTestId("dialog-proposal")).toBeInTheDocument();
      expect(scrolled).not.toHaveBeenCalled();
      expect(screen.getByTestId("dialog-pane")).not.toHaveAttribute("data-inspected");
    } finally {
      scrolled.mockRestore();
    }
  });

  // Found by reviewing this phase's own diff: every test above asserts the mark ARRIVES, and a
  // mark that never leaves would pass all of them while ringing the pane for the rest of the
  // session. It is an acknowledgement of one click, so it is over when the click is.
  it("SpecDialog_TheMark_ClearsItselfWithoutAnotherAct", async () => {
    await proposedAndFiled();

    inspectTheCard();
    expect(screen.getByTestId("dialog-pane")).toHaveAttribute("data-inspected", "true");

    await waitFor(
      () => expect(screen.getByTestId("dialog-pane")).not.toHaveAttribute("data-inspected"),
      { timeout: 4000 },
    );
    // The focus it moved is NOT transient: the reader is still where the act put them.
    expect(document.activeElement).toBe(screen.getByRole("tabpanel"));
  });

  // Minus one and not zero: the panel is a destination for an act elsewhere on the page, not a
  // stop a keyboard has to pass through on its way to the controls inside it.
  it("DialogPane_ThePanel_IsFocusableWithoutJoiningTheTabOrder", async () => {
    await renderSurface();

    expect(screen.getByRole("tabpanel")).toHaveAttribute("tabindex", "-1");
  });
});
