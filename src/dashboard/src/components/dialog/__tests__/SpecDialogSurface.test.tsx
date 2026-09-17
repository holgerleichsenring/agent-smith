import { render, screen, fireEvent, waitFor, cleanup, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SpecDialogSurface } from "../SpecDialogSurface";
import { __forgetDialogIdForTests } from "@/lib/specDialogSession";
import type {
  SpecDialogFilingPush,
  SpecDialogMessagePush,
  SpecDialogPhaseProposal,
  SpecDialogProposalPush,
  SpecDialogQuestionPush,
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
const subscribeSpecDialog = vi.fn(async () => async () => {});


vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: messages,
    specDialogQuestions: questions,
    specDialogProposals: proposals,
    specDialogFilings: filings,
    subscribeSpecDialog,
  }),
}));

const fetchSpecDialog = vi.fn();
const postSpecDialogMessage = vi.fn(async () => {});
vi.mock("@/lib/specDialogApi", () => ({
  fetchSpecDialog: (dialogId: string) => fetchSpecDialog(dialogId),
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
    openSessions: [],
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
    ...overrides,
  };
}

function filing(overrides: Partial<SpecDialogFilingPush> = {}): SpecDialogFilingPush {
  return {
    dialogId: heldDialogId(),
    filed: [{ reference: "https://tracker/7", title: "p9001: the phase" }],
    error: null,
    at: "2026-09-15T10:04:00Z",
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
  postSpecDialogMessage.mockClear();
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

  it("SpecDialog_AnOpenSessionElsewhere_IsResumedByClickingIt", async () => {
    fetchSpecDialog.mockResolvedValue(view({
      session: null,
      openSessions: [
        { sessionId: "s-9", project: "sample", turns: 3, lastActivityAt: "2026-09-15T09:00:00Z" },
      ],
    }));
    await renderSurface();

    fireEvent.click(screen.getByTestId("dialog-resume-s-9"));

    await waitFor(() =>
      expect(postSpecDialogMessage).toHaveBeenCalledWith(heldDialogId(), "/spec resume s-9"));
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

    expect(await screen.findByTestId("dialog-proposal-phase-p9000")).toHaveTextContent(
      "goal of p9000",
    );
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

  it("SpecDialog_APushForAnotherDialog_ChangesNothing", async () => {
    await renderSurface();

    act(() => proposals.emit(proposal({ dialogId: "someone-elses-dialog" })));

    expect(screen.queryByTestId("dialog-proposal")).not.toBeInTheDocument();
    expect(screen.getByTestId("dialog-scope")).toBeInTheDocument();
  });
});
