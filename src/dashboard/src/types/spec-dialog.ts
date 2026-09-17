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

/** 2026-09-17-042el: what an operator's answer to an approval question decided. */
export type SpecDialogDecision = "approved" | "rejected";

export interface SpecDialogTurn {
  /** "user" or "assistant" — the durable transcript's own word. */
  role: string;
  text: string;
  at: string;
  /** Set on the operator turn that answered an approval question with approve or reject. */
  decision?: SpecDialogDecision | null;
}

export interface SpecDialogSession {
  sessionId: string;
  scope: SpecDialogProject;
  /** Assistant turns come without their draft — the pane shows it; operator turns as written. */
  transcript: SpecDialogTurn[];
  lastActivityAt: string;
  /** The proposal under discussion; null when nothing is, including after a rejection. */
  proposal: SpecDialogProposalPush | null;
  /** What the latest filing created. Older than `proposal` means it filed an earlier one. */
  filing: SpecDialogFilingPush | null;
  /** The index in `transcript` of the turn the proposal card belongs on. */
  proposalTurn: number | null;
}

/** What a conversation's latest filing created. Only a filing produces one. */
export interface SpecDialogConversationOutcome {
  /** "bug", "phase" or "epic" from the latest proposal; null once that was rejected. */
  kind: string | null;
  tickets: number;
  /** The filing stopped with an error after creating some of the tickets. */
  partial: boolean;
}

/** One of the caller's conversations, open or closed, addressed by its session id. */
export interface SpecDialogSessionSummary {
  sessionId: string;
  project: string;
  turns: number;
  lastActivityAt: string;
  /** The first line the operator wrote; null before they wrote one. */
  title: string | null;
  outcome: SpecDialogConversationOutcome | null;
  /** The dialog id an open conversation lives on; null once it is closed. */
  openDialogId: string | null;
}

/** What the surface needs for one dialog id, re-read after every message. The caller's
 *  conversation list is a read of its own, because it reads every listed transcript. */
export interface SpecDialogView {
  dialogId: string;
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
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

/** How far a design turn got with opening one repository. */
export type SpecDialogReadingState = "opening" | "ready" | "failed";

/** 2026-09-17-c7aec: one repository a running design turn opened, pushed as it happens. */
export interface SpecDialogReadingPush {
  dialogId: string;
  repo: string;
  state: SpecDialogReadingState;
  at: string;
}

// 2026-09-15-6d9c: the turn's typed outcome, and what filing it actually created — the two
// pushes the right-hand column changes state on. Plain payloads rather than hub events: the
// event-type generator scans the events namespace by base type, and these derive from
// neither base.

/** One drafted phase as the proposal pane renders it. */
export interface SpecDialogPhaseProposal {
  phaseId: string;
  goal: string;
  /** The step actions, in spec order. */
  steps: string[];
  tests: string[];
  done: string[];
  requires: string[];
  /** The spec as the master wrote it — the raw form the reply no longer carries. */
  yaml: string;
}

/** The fix-bug ticket a bug outcome would file — body exactly as the filer composes it. */
export interface SpecDialogBugProposal {
  title: string;
  body: string;
}

/**
 * What this turn would file. An answer proposes nothing and is never pushed, so the pane
 * keeps whatever is still under discussion.
 */
export interface SpecDialogProposalPush {
  dialogId: string;
  /** "bug", "phase" or "epic" — the pane renders by it. */
  kind: string;
  bug: SpecDialogBugProposal | null;
  phase: SpecDialogPhaseProposal | null;
  parent: SpecDialogPhaseProposal | null;
  /** An epic's children IN THE ORDER THEY WILL BE FILED. */
  children: SpecDialogPhaseProposal[];
  at: string;
}

/** One ticket that was actually created. `reference` is a web URL where the provider gives one. */
export interface SpecDialogFiledTicket {
  reference: string;
  title: string;
}

/**
 * What the filing attempt did. `filed` carries every ticket that was created even when
 * `error` is set — a partial epic must never silently lose the children it did create.
 */
export interface SpecDialogFilingPush {
  dialogId: string;
  filed: SpecDialogFiledTicket[];
  error: string | null;
  at: string;
}
