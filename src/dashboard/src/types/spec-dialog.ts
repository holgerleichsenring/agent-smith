// 2026-09-15-cb3e: the wire shapes of the spec dialog as the dashboard reads them — the
// GET the page opens with, and the two hub pushes the conversation arrives through. Kept
// by hand (unlike types/hub-events.ts, whose generator walks Contracts/Events only).

/** One template a turn of this conversation may read. */
export interface SpecDialogTemplate {
  name: string;
  repo: string;
  revision: string;
}

/** What a conversation on this project is grounded in. */
export interface SpecDialogProject {
  name: string;
  repos: string[];
  templates: SpecDialogTemplate[];
}

export interface SpecDialogTurn {
  /** "user" or "assistant" — the durable transcript's own word. */
  role: string;
  text: string;
  at: string;
}

export interface SpecDialogSession {
  sessionId: string;
  scope: SpecDialogProject;
  transcript: SpecDialogTurn[];
  lastActivityAt: string;
}

export interface SpecDialogSessionSummary {
  sessionId: string;
  project: string;
  turns: number;
  lastActivityAt: string;
}

/** Everything the surface needs for one dialog id, in one read. */
export interface SpecDialogView {
  dialogId: string;
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
  openSessions: SpecDialogSessionSummary[];
  /** What the turn is blocked on, so a reload during the gate keeps the question. */
  question: SpecDialogQuestionPush | null;
}

/** A reply delivered into the session group. */
export interface SpecDialogMessagePush {
  dialogId: string;
  title: string;
  text: string;
  at: string;
}

export interface SpecDialogChoice {
  label: string;
  description?: string | null;
}

/**
 * A question the dialog is waiting on. `kind` is the question's TYPE — the approval
 * that files tickets carries no choices at all, so the controls come from the kind.
 */
export interface SpecDialogQuestionPush {
  dialogId: string;
  questionId: string;
  kind: string;
  text: string;
  choices: SpecDialogChoice[];
  at: string;
  /** When the wait ends; null where nothing ends it but the next message. Nothing is
   *  pushed when one expires, so without this the card offers a button whose click has
   *  stopped being an answer and becomes a whole design turn on the word "approve". */
  expiresAt: string | null;
}
