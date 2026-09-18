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

/** What a running design turn was doing when it reported one step. */
export type SpecDialogActivityKind = "tool" | "model" | "reviewing" | "revising";

/** 2026-09-17-042ee: one step a running design turn took, pushed as it happens. A tool
 *  carries its name and a whitelisted argument summary; a model call carries the model and
 *  the intent it narrated; reviewing and revising are states and carry neither. */
export interface SpecDialogActivityPush {
  dialogId: string;
  kind: SpecDialogActivityKind;
  name: string | null;
  detail: string | null;
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
 * 2026-09-17-042ed: one thing the turn's own review found against the proposal. `evidence` is the
 * framework-minted line of the look it rests on, already resolved from the id it cited; `quote` is
 * what the phase states, for a finding that cites nothing. Hand-written, as the push it rides on is.
 */
export interface SpecDialogProposalFinding {
  phaseId: string;
  problem: string;
  why: string;
  quote: string | null;
  evidence: string | null;
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
  /** What the turn's review found; empty for a clean review and for one that could not be taken,
   * and absent altogether on a proposal stored before the review shipped. */
  findings?: SpecDialogProposalFinding[];
}

/**
 * 2026-09-17-042eg: what a filed ticket became. `Started` means a run will pick it up — the
 * poller's own envelope resolves it to the filing project and it sits in a trigger status.
 * `NotStarted` says why nothing will, and `Record` is a slice record, which is not work at all.
 */
export interface SpecDialogFiledStart {
  state: "Started" | "NotStarted" | "Record";
  reason: string;
}

/** One ticket that was actually created. `reference` is a web URL where the provider gives one. */
export interface SpecDialogFiledTicket {
  reference: string;
  title: string;
  /** The tracker-native id and the project it was filed into. Absent on a filing written
   * before 2026-09-17-042eg, which reads as unknown rather than as a wrong answer. */
  ticketId?: string | null;
  project?: string | null;
  /** 2026-09-17-042em: what a person calls it — the Jira key, or the tracker's number behind a
   * hash. Absent on a filing written before that phase, which reads by its reference as before. */
  key?: string | null;
  /** Absent on a filing written before this phase — the panel then says nothing about it. */
  start?: SpecDialogFiledStart | null;
}

/**
 * What the filing attempt did. `filed` carries every ticket that was created even when
 * `error` is set — a partial epic must never silently lose the children it did create.
 */
export interface SpecDialogFilingPush {
  dialogId: string;
  filed: SpecDialogFiledTicket[];
  error: string | null;
  /** What went wrong without unfiling anything — a child the tracker would not link to its parent. */
  notes?: string[];
  at: string;
}

// 2026-09-17-042ej: what the conversation that filed work is now following. The rows are
// ticket, then run, then PHASE — the unit an operator reasons in. Read over a route of its
// own and refetched on a data-free nudge; nothing about a run arrives over the hub.

/** One thing 2026-09-17-042eh's review kept against a phase's own diff. */
export interface FiledWorkFinding {
  repository: string;
  path: string;
  line: number;
  rule: string;
  why: string;
  cites?: string | null;
  /** Set when the fix pass meant to close this was reverted — the branch still holds the code. */
  reverted?: string | null;
}

/**
 * What came of one phase's review, in four states. `reviewed` true is a fresh instance that
 * read the diff — with or without findings. `reviewed` false is NOT a clean review: nobody
 * was asked, and `why` says which reason it was. `unreadable` is a row that exists and cannot
 * be deserialized. A null review on the phase is the fourth: no row at all, so it never
 * reached its review step.
 */
export interface FiledWorkReview {
  reviewed: boolean;
  why: string | null;
  findings: FiledWorkFinding[];
  unreadable: boolean;
}

/** One phase of the run, as its own row says it stands. */
export interface FiledWorkPhase {
  phaseId: string;
  ordinal: number;
  title: string;
  /** not_started | in_progress | done | failed. */
  status: string;
  /** Only ever set on a terminal row: a verdict is sticky and a rerun would show the old one. */
  verdict: string | null;
  review: FiledWorkReview | null;
}

/** One pull request the run recorded, per repository. Merge state is read on the tracker. */
export interface FiledWorkPullRequest {
  repo: string;
  status: string;
  url: string | null;
  reason: string | null;
  openedAt: string;
}

/** The question a parked run is waiting on, as the run view already shapes it. */
export interface FiledWorkQuestion {
  questionId: string;
  text: string;
  askedAt: string;
  answerDeadlineAt: string;
}

/** One run of the work ticket, in one of the projects sharing the filing project's tracker. */
export interface FiledWorkRun {
  runId: string;
  project: string;
  pipeline: string;
  status: string;
  costUsd: number;
  startedAt: string;
  finishedAt: string | null;
  pullRequests: FiledWorkPullRequest[];
  phases: FiledWorkPhase[];
  pendingQuestion: FiledWorkQuestion | null;
}

/** The hand-back case the ticket's spec set last recorded; the words live on the ticket. */
export interface FiledWorkHandback {
  case: string;
  repeated: number;
}

/** One filed ticket with the runs that took it up. A slice record carries none. */
export interface FiledWorkTicket {
  reference: string;
  key: string | null;
  title: string;
  ticketId: string | null;
  project: string | null;
  start: SpecDialogFiledStart | null;
  runs: FiledWorkRun[];
  handback: FiledWorkHandback | null;
}

/** The whole read. Empty for a dialog id with no open session. */
export interface FiledWork {
  dialogId: string;
  tickets: FiledWorkTicket[];
}
