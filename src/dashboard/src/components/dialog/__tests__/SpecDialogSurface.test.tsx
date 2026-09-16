import { render, screen, fireEvent, waitFor, cleanup, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SpecDialogSurface } from "../SpecDialogSurface";
import { __forgetDialogIdForTests } from "@/lib/specDialogSession";
import type {
  SpecDialogMessagePush,
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
const subscribeSpecDialog = vi.fn(async () => async () => {});


vi.mock("@/lib/JobsHubClient", () => ({
  getJobsHubClient: () => ({
    specDialogMessages: messages,
    specDialogQuestions: questions,
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
});
