import { render, renderHook, screen, fireEvent, waitFor, cleanup, act, within } from "@testing-library/react";
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
  SpecDialogView,
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


vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: messages,
    specDialogQuestions: questions,
    specDialogProposals: proposals,
    specDialogFilings: filings,
    specDialogReadings: readings,
    specDialogActivity: activity,
    subscribeSpecDialog,
  }),
}));

const fetchSpecDialog = vi.fn();
const postSpecDialogMessage = vi.fn<(dialogId: string, text: string) => Promise<void>>(async () => {});
const fetchSpecDialogConversations = vi.fn();
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (dialogId: string) => fetchSpecDialog(dialogId),
  fetchSpecDialogConversations: () => fetchSpecDialogConversations(),
  postSpecDialogMessage: (dialogId: string, text: string) =>
    postSpecDialogMessage(dialogId, text),
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
    session: {
      sessionId: "s-1",
      scope: SAMPLE_SCOPE,
      transcript: [],
      lastActivityAt: "2026-09-15T10:00:00Z",
      proposal: null,
      filing: null,
      proposalTurn: null,
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
  stubStorage();
  __forgetDialogIdForTests();
  fetchSpecDialog.mockReset();
  fetchSpecDialog.mockResolvedValue(view());
  fetchSpecDialogConversations.mockReset();
  fetchSpecDialogConversations.mockResolvedValue([]);
  postSpecDialogMessage.mockReset();
  subscribeSpecDialog.mockClear();
});

afterEach(() => cleanup());

async function renderSurface() {
  render(<SpecDialogSurface />);
  await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());
  await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
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
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "the reader"));
  });

  it("SpecDialog_TheApprovalGate_OffersApproveAndRejectThoughItCarriesNoChoices", async () => {
    await renderSurface();

    act(() => questions.emit(question()));

    fireEvent.click(await screen.findByTestId("dialog-answer-approve"));

    expect(screen.getByTestId("dialog-answer-reject")).toBeInTheDocument();
    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "approve"));
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

  it("SpecDialog_TheScopeColumn_NamesTheRepositoriesAndTheTemplates", async () => {
    await renderSurface();

    const scope = screen.getByTestId("dialog-scope");
    expect(scope).toHaveTextContent("repo-a");
    expect(scope).toHaveTextContent("template:default");
    expect(scope).toHaveTextContent("v1.2");
  });

  it("SpecDialog_NewConversation_OpensOnThePickedProjectUnderAFreshDialogId", async () => {
    const other = { name: "other", repos: [], templates: [] };
    fetchSpecDialog.mockResolvedValue(view({ session: null, projects: [SAMPLE_SCOPE, other] }));
    await renderSurface();
    const first = heldDialogId();

    fireEvent.change(screen.getByTestId("dialog-project-picker"), {
      target: { value: "other" },
    });
    fireEvent.click(screen.getByTestId("dialog-new"));

    await waitFor(() => expect(heldDialogId()).not.toBe(first));
    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "/spec other"));
  });

  function conversation(overrides: Partial<SpecDialogSessionSummary> = {}): SpecDialogSessionSummary {
    return {
      sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: "2026-09-15T09:00:00Z",
      title: "a widget that reads the ledger", outcome: null, openDialogId: null,
      ...overrides,
    };
  }

  it("SpecDialog_TheConversationList_NamesEachByTitleAndWhatItFiled", async () => {
    fetchSpecDialogConversations.mockResolvedValue([
      conversation({ outcome: { kind: "epic", tickets: 3, partial: true } }),
    ]);
    await renderSurface();

    const row = await screen.findByTestId("dialog-conversation-s-9");
    expect(row).toHaveTextContent("a widget that reads the ledger");
    expect(row).toHaveTextContent("sample");
    expect(within(row).getByTestId("dialog-conversation-outcome")).toHaveTextContent(/^epic partly filed · 3 tickets$/);
  });

  // 2026-09-17-c7aed: the list reads as a history, by the calendar day of the last thing said.
  it("SpecDialog_TheConversations_GroupByDayAndMarkTheCurrentOne", async () => {
    const now = new Date();
    const daysAgo = (days: number) =>
      new Date(now.getFullYear(), now.getMonth(), now.getDate() - days, 12).toISOString();
    fetchSpecDialogConversations.mockResolvedValue([
      conversation({ sessionId: "s-1", lastActivityAt: now.toISOString() }),
      conversation({ sessionId: "s-2", lastActivityAt: daysAgo(1) }),
      conversation({ sessionId: "s-3", lastActivityAt: daysAgo(3) }),
      conversation({ sessionId: "s-4", lastActivityAt: daysAgo(30) }),
    ]);
    await renderSurface();

    await screen.findByTestId("dialog-conversation-s-4");
    const days = screen.getAllByTestId("dialog-conversation-day");
    expect(days.map((day) => [
      day.querySelector(":scope > h3")?.textContent,
      [...day.querySelectorAll("button")].map((row) => row.dataset.testid),
    ])).toEqual([
      ["Today", ["dialog-conversation-s-1"]],
      ["Yesterday", ["dialog-conversation-s-2"]],
      ["Last week", ["dialog-conversation-s-3"]],
      ["Earlier", ["dialog-conversation-s-4"]],
    ]);
    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveAttribute("aria-current", "true");
    expect(screen.getByTestId("dialog-conversation-s-2")).not.toHaveAttribute("aria-current");
  });

  it("SpecDialog_AConversationWithAnOutcome_ShowsIt_OneWithoutShowsNone", async () => {
    fetchSpecDialogConversations.mockResolvedValue([
      conversation({ sessionId: "s-7", outcome: { kind: "bug", tickets: 1, partial: false } }),
      conversation({ sessionId: "s-8", outcome: null }),
    ]);
    await renderSurface();

    const filed = await screen.findByTestId("dialog-conversation-s-7");
    expect(within(filed).getByTestId("dialog-conversation-outcome")).toHaveTextContent("bug filed");
    expect(within(screen.getByTestId("dialog-conversation-s-8"))
      .queryByTestId("dialog-conversation-outcome")).toBeNull();
  });

  // The spec said every opening resumes onto a fresh dialog id; since 2026-09-17-c7aeb's review
  // only a CLOSED conversation does — an open one is returned to (the test below this one).
  it("SpecDialog_OpeningAClosedConversation_ResumesOntoAFreshDialogIdAndClosesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue([conversation({ openDialogId: null })]);
    await renderSurface();
    const first = heldDialogId();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-9"));

    await waitFor(() => expect(postSpecDialogMessage).toHaveBeenCalled());
    const fresh = heldDialogId();
    expect(fresh).not.toBe(first);
    expect(postSpecDialogMessage.mock.calls).toEqual([[fresh, "/spec resume s-9"]]);
  });

  // 2026-09-17-c7aeb: a dialog id is a tab, not a conversation. Opening a past one mints a
  // fresh id and resumes onto it, and that id's first read comes BEFORE the resume has moved
  // anything there — a re-seed spent on it left the page with no transcript at all.
  // Found by review: a conversation left mid-turn could not be reopened. The click was a resume,
  // a resume is refused while a turn runs, and the page had already left the tab the reply and a
  // waiting approval were going to. An open conversation is somewhere; the page goes there.
  it("SpecDialog_OpeningAnOpenConversation_ReturnsToItsDialogAndResumesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue([conversation({ openDialogId: "d-where-it-lives" })]);
    await renderSurface();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-9"));

    await waitFor(() => expect(heldDialogId()).toBe("d-where-it-lives"));
    expect(postSpecDialogMessage.mock.calls.map((call) => call[1]))
      .not.toContainEqual(expect.stringMatching(/^\/spec resume/));
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
    postSpecDialogMessage.mockImplementation(async (...[, text]) => {
      if (text.startsWith("/spec resume")) resumed = true;
    });
    fetchSpecDialogConversations.mockResolvedValue([conversation()]);
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
      expect(postSpecDialogMessage).toHaveBeenCalledWith(fresh, "/spec resume s-9"));
    await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalledWith(fresh));
    act(() => messages.emit({
      dialogId: fresh, title: "Spec dialog",
      text: "Spec dialog `s-9` resumed — scope **sample**, 1 turn(s) so far.",
      at: "2026-09-15T10:06:00Z",
    }));

    expect(await screen.findByTestId("dialog-turn-user")).toHaveTextContent(
      "a widget that reads the ledger",
    );
    expect(postSpecDialogMessage).not.toHaveBeenCalledWith(first, expect.anything());
  });

  it("SpecDialog_ClickingTheConversationAlreadyOpen_DoesNothing", async () => {
    fetchSpecDialogConversations.mockResolvedValue([conversation({ sessionId: "s-1" })]);
    await renderSurface();
    const first = heldDialogId();

    fireEvent.click(await screen.findByTestId("dialog-conversation-s-1"));
    await act(async () => {});

    expect(heldDialogId()).toBe(first);
    expect(postSpecDialogMessage).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-conversation-s-1")).toHaveAttribute("aria-current", "true");
  });

  it("SpecDialog_TheConversationList_IsNotReadOnEveryMessage", async () => {
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

  it("SpecDialog_AFiling_RereadsTheConversationList", async () => {
    await renderSurface();
    await waitFor(() => expect(fetchSpecDialogConversations).toHaveBeenCalled());
    const listed = fetchSpecDialogConversations.mock.calls.length;

    act(() => filings.emit(filing()));

    await waitFor(() =>
      expect(fetchSpecDialogConversations.mock.calls.length).toBeGreaterThan(listed));
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
      activity.emit({ dialogId: heldDialogId(), kind: "tool", name: "read_file", detail: "repo-a/src/A.cs", at });
      activity.emit({ dialogId: heldDialogId(), kind: "tool", name: "grep_in_files", detail: "Dispatch", at });
      activity.emit({ dialogId: heldDialogId(), kind: "model", name: "sample-model", detail: "Checking the router.", at });
      activity.emit({ dialogId: heldDialogId(), kind: "reviewing", name: null, detail: null, at });
      activity.emit({ dialogId: "someone-else", kind: "tool", name: "read_file", detail: "foreign", at });
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

  it("SpecDialog_WhenTheAnswerArrives_ForgetsWhatTheTurnDid", async () => {
    const { result } = renderHook(() => useSpecDialog());
    await waitFor(() => expect(subscribeSpecDialog).toHaveBeenCalled());
    await act(() => result.current.send("update every dependency"));
    const dialogId = result.current.dialogId!;
    act(() => activity.emit({
      dialogId, kind: "revising", name: null, detail: null, at: new Date().toISOString(),
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

  // Writing with no session open reached the router as an ordinary message, and the router
  // answered with the command tutorial a chat channel needs — on a page whose whole point
  // is that nobody types a command.
  it("SpecDialog_SendingWithNoSessionOpen_OpensOneOnThePickedProjectFirst", async () => {
    fetchSpecDialog.mockResolvedValue(view({ session: null }));
    render(<SpecDialogSurface />);
    await waitFor(() => expect(fetchSpecDialog).toHaveBeenCalled());

    fireEvent.change(screen.getByTestId("dialog-composer-text"), {
      target: { value: "update every dependency" },
    });
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    await waitFor(() => expect(postSpecDialogMessage.mock.calls.map((c) => c[1]))
      .toEqual(["/spec sample", "update every dependency"]));
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

  it("SpecDialog_ARunningTurn_NamesEachRepositoryItOpensAndNoDuration", async () => {
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
    expect(working).not.toHaveTextContent(/minute/);
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
      .toHaveTextContent(/^File this epic\? One work ticket and 2 slice records\.$/);
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
      .toHaveTextContent(/^File this epic\? One work ticket and 2 slice records\.$/);
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
      .toHaveTextContent(/^File this epic\? One work ticket and 2 slice records\.$/);
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

  it("SpecDialog_APushForAnotherDialog_ChangesNothing", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal({ dialogId: "someone-elses-dialog" })));

    expect(screen.queryByTestId("dialog-proposal")).not.toBeInTheDocument();
    expect(screen.getByTestId("dialog-scope")).toBeInTheDocument();
  });
});
