import type { PendingQuestionInfo } from "@/types/hub-events";

// 2026-10-13-9a59p: the ticket list as GET /api/tickets serves it (TicketListView and the records
// under it in AgentSmith.Server/Models, camelCase). Fields a later member of the series added are
// optional: an older server omits them, and the list draws nothing rather than a guess.

/** The server's buckets, exactly as TicketBucket spells them. */
export type TicketServerBucket =
  | "needs_you"
  | "working"
  | "waiting_on_review"
  | "stuck"
  | "idle"
  | "done";

/** The buckets as the URL spells them (?bucket=), in the order the list shows them. */
export const TICKET_BUCKETS = ["needs-you", "working", "waiting-on-review", "stuck", "ended", "done"] as const;
export type TicketBucket = (typeof TICKET_BUCKETS)[number];

/** URL bucket -> the server's word. "ended" is the server's idle: no bucket owns the ticket. */
export const SERVER_BUCKET: Readonly<Record<TicketBucket, TicketServerBucket>> = {
  "needs-you": "needs_you",
  working: "working",
  "waiting-on-review": "waiting_on_review",
  stuck: "stuck",
  ended: "idle",
  done: "done",
};

/** One attempt as the row marks it, oldest first. */
export interface TicketAttemptMark {
  runId: string;
  status: string;
  endedOnInfrastructure: boolean;
  handedBack: boolean;
}

/** One pull request of a ticket, as its review record says. */
export interface TicketPullRequest {
  number: string;
  url: string;
  repo: string;
  /** open | merged | closed | unsupported | unread */
  state: string;
  /** none | approved | changes-requested | changes-requested-untrusted | rejected */
  decision: string;
  by: string | null;
  lastActivityAt: string | null;
  stateAt: string | null;
  note: string | null;
}

export interface AttemptSource {
  kind: string;
  url?: string | null;
  id?: string | null;
  detail?: string | null;
}

/** Why the newest attempt with a record began (AttemptChannels). */
export interface AttemptCause {
  channel: string;
  actor?: string | null;
  at?: string | null;
  source?: AttemptSource | null;
}

/** done | carried | live | asked | failed | handed_back | not_started */
export type TicketSeriesCellState = string;

export interface TicketSeriesStrip {
  /** cut | deriving | handed_back | none */
  derive: string;
  cells: Array<{ specId: string; state: TicketSeriesCellState }>;
}

/** A needs-you question row's question and the run that asks it. */
export interface TicketPendingQuestion {
  runId: string;
  question: PendingQuestionInfo;
}

export interface TicketSummary {
  project: string;
  ticketId: string;
  ticketKey: string | null;
  platform: string | null;
  title: string | null;
  bucket: TicketServerBucket;
  /** question | handback on a needs-you row */
  kind: string | null;
  since: string | null;
  attemptCount: number;
  cost: number;
  newestRunId: string;
  status: string;
  lastOutcome: string | null;
  queuePlace: number | null;
  pendingQuestion: TicketPendingQuestion | null;
  specsDone: number;
  specsTotal: number;
  attempts: TicketAttemptMark[];
  pullRequests?: TicketPullRequest[];
  cause?: AttemptCause | null;
  trackerState?: string | null;
  webUrl?: string | null;
  seriesStrip?: TicketSeriesStrip | null;
  unchangedSinceLastAttempt?: boolean | null;
  checkedAt?: string | null;
  handback?: { text: string; at: string } | null;
  /** merged | unmerged once every pull request ended; null while any is open or unread */
  landing?: string | null;
}

export interface TicketCounts {
  needsYou: number;
  working: number;
  running: number;
  queued: number;
  idle: number;
  oldestNeedsYouSince: string | null;
  monthCost: number;
  truncated: boolean;
  waitingOnReview: number;
  done: number;
  openPullRequests: number;
  doneThisMonth: { tickets: number; attempts: number };
  lastDone: { project: string; ticketId: string; at: string } | null;
  stuck: number;
}

export interface TicketListView {
  tickets: TicketSummary[];
  nextCursor: string | null;
  counts: TicketCounts;
}

// 2026-10-13-9a59q: the ticket page's reads — GET /api/tickets/{project}/{ticketId} (TicketDetailView),
// .../series[?runId=] (TicketSeriesView), .../conversation (TicketConversationView) and
// GET /api/runs/{runId}/passes (RunPassesView), camelCase as AgentSmith.Server/Models serves them.

/** A per-repository pull-request outcome as the run row recorded it (opened | no_changes | failed). */
export interface TicketRunPullRequest {
  repo: string;
  status: string;
  url: string | null;
  reason: string | null;
  openedAt: string;
}

/** A spec the attempt executed (a RunSpec row, not_started included). */
export interface TicketExecutedSpec {
  specId: string;
  ordinal: number;
  title: string;
  status: string;
  verdict: string | null;
}

export interface TicketAttemptRevision {
  number: number;
  cause: string | null;
  seriesId: string | null;
  carriedIds: string[];
  approvalAt: string | null;
}

/** One attempt at the ticket, numbered from 1 by run id. */
export interface TicketAttempt {
  number: number;
  runId: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  cost: number;
  endedOnInfrastructure: boolean;
  handedBack: boolean;
  pullRequests: TicketRunPullRequest[];
  executedSpecs: TicketExecutedSpec[];
  pendingQuestion: PendingQuestionInfo | null;
  cause?: AttemptCause | null;
  revision?: TicketAttemptRevision | null;
}

export interface TicketOtherRun {
  runId: string;
  pipeline: string;
  status: string;
  startedAt: string;
  finishedAt: string | null;
  cost: number;
  neverStarted: boolean;
}

export interface TicketActedSince {
  kind: string;
  who: string | null;
  at: string;
  url?: string | null;
}

export interface TicketDetail {
  summary: TicketSummary;
  branch: string;
  attempts: TicketAttempt[];
  otherRuns: TicketOtherRun[];
  actedSince?: TicketActedSince[] | null;
}

export interface TicketSpecStep {
  id: string;
  action: string;
  target: string | null;
}

export interface TicketSeriesCriterion {
  text: string;
  /** met | unmet | not_applicable | unproven; null while unjudged */
  verdict: string | null;
  note: string | null;
  citation: string | null;
  overrule: { humanStatus: string; reason: string; author: string; at: string } | null;
}

export interface TicketSeriesStepWorked {
  stepId: string;
  state: string | null;
  doneByRunId: string | null;
  doneByAttempt: number | null;
  doneInPass: number | null;
}

export interface TicketSeriesSpec {
  specId: string;
  ordinal: number;
  /** Null for a spec read from the run record: no cut named its file. */
  fileStem: string | null;
  goal: string;
  repos: string[];
  steps: TicketSpecStep[];
  yaml: string;
  /** not_started | in_progress | done | failed | handed_back | carried */
  state: string;
  executedBy: string | null;
  executedByAttempt: number | null;
  done: TicketSeriesCriterion[];
  unmeasured: Array<{ repositories: string; reason: string | null }>;
  worked?: TicketSeriesStepWorked[] | null;
  /** False when only the goal was recorded; recordNote says why. */
  hasRecord?: boolean;
  /** "no record" | "record unreadable"; null with a record. */
  recordNote?: string | null;
  /** False for a terminal spec none of whose criteria carries a verdict. */
  verdictRecorded?: boolean;
}

/** Run acceptance a run-record read could not tie to one spec. */
export interface TicketSeriesUnattributed {
  runId: string;
  attempt: number;
  criteria: TicketSeriesCriterion[];
  unmeasured: Array<{ repositories: string; reason: string | null }>;
}

export interface TicketSeries {
  project: string;
  ticketId: string;
  /** The attempt the cut came from; null when no attempt published one. */
  runId: string | null;
  attempt: number | null;
  at: string | null;
  setKey: string | null;
  handback: { case: string; runId: string; attempt: number; at: string } | null;
  /** passed | failed | skipped */
  gate: { status: string; specCount: number; runId: string } | null;
  specs: TicketSeriesSpec[];
  revisions: Array<{ number: number; cause: string | null; runId: string; attempt: number; at: string | null }>;
  recorded: boolean;
  /** cut | run-record — run-record when the attempts predate the cut and the specs come from their rows. */
  source?: string;
  unattributedCriteria?: TicketSeriesUnattributed[] | null;
}

export interface RunPassCost {
  costUsd: number;
  calls: number;
}

export interface RunPassCriterion {
  criterion: string;
  /** met | not_applicable | unmet */
  status: string;
  evidence: string;
}

export interface RunPassStepMoved {
  entryId: string;
  activity: string;
  from: string | null;
  to: string | null;
  cause: string;
  pass: number;
}

/** One master pass; ordinal is run-wide, passInSpec the number a reader is shown. */
export interface RunPass {
  ordinal: number;
  passInSpec: number;
  passInStep: number | null;
  openedBecause: string | null;
  endedBecause: string;
  reengageReason: string | null;
  verdictEmitted: boolean | null;
  criteriaMet: number | null;
  criteriaTotal: number | null;
  criteria: RunPassCriterion[] | null;
  gateLine: string | null;
  actionablePending: number | null;
  unbackedDone: number | null;
  endedAt: string | null;
  stepsMoved: RunPassStepMoved[];
  costUsd: number;
  calls: number;
}

export interface RunPassSpec {
  specId: string;
  passes: RunPass[];
  outsidePasses: RunPassCost;
}

export interface RunPasses {
  runId: string;
  specs: RunPassSpec[];
  derive: RunPassCost;
  pr: RunPassCost;
  other: RunPassCost;
  notAttributed: RunPassCost;
}

/** ticket | question | pr-review | spec-cut | conversation */
export type TicketConversationSource = string;

/** How a tracker or conversation text was written, as its source names it. */
export type TicketTextFormat = "html" | "markdown" | "text";

/** Where the ticket text came from: its tracker now, an attempt's copy, or neither. */
export type TicketTextSource = "tracker" | "snapshot" | "unavailable";

/** Why the tracker could not be read — a class, never the exception's text. */
export type TicketTextReason = "not-found" | "unauthorized" | "unreachable" | "project-not-configured";

/** GET /api/tickets/{project}/{ticketId}/text — the ticket itself. */
export interface TicketText {
  source: TicketTextSource;
  title: string | null;
  description: string | null;
  descriptionFormat: TicketTextFormat;
  acceptanceCriteria: string | null;
  criteriaFormat: TicketTextFormat;
  workItemType: string | null;
  state: string | null;
  labels: string[] | null;
  assignee: string | null;
  webUrl: string | null;
  /** When the tracker was read, or when the attempt kept its copy. */
  at: string | null;
  runId?: string | null;
  attempt?: number | null;
  reason?: TicketTextReason | null;
}

export interface TicketConversationEntry {
  id: string;
  sourceId: string | null;
  prKey: string | null;
  source: TicketConversationSource;
  /** edited | answer | default-answer | command-answer | comment | turn | question | hand-back | cut-review | … */
  kind: string;
  /** person | agent | system | reviewer */
  role: string;
  author: string | null;
  isOurs: boolean;
  at: string;
  attemptRunId: string | null;
  group: string | null;
  text: string | null;
  file?: string | null;
  line?: number | null;
  detail?: unknown;
  url?: string | null;
  format: TicketTextFormat;
}

export interface TicketConversationAttempt {
  runId: string;
  number: number;
  cause: AttemptCause | null;
  revision: TicketAttemptRevision | null;
  demandEntryId?: string | null;
}

export interface TicketConversation {
  project: string;
  ticketId: string;
  entries: TicketConversationEntry[];
  attempts: TicketConversationAttempt[];
  sessionId: string | null;
  openDialogId: string | null;
  proposal: {
    kind: string;
    drafts: Array<{ specId: string; goal: string; repos: string[]; done: string[] }>;
  } | null;
  transcriptHidden: boolean;
  threadUnavailable: boolean;
}

// 2026-10-13-9a59r: what approving the ticket's open proposal would do (GET …/approval-preview,
// dialog.write; 404 when no proposal is open) — 9a59l's effect and per-spec dispositions, 9a59m's start arm.

/** bug-ticket | follow-up-closed | amend-never-worked | refused-over-cap | refused | continue */
export type TicketApprovalEffect = string;

/** start-now | wait-for-run | refused-question-pending | recorded-without-runs-control | answers-handback */
export type TicketStartArm = string;

export interface TicketContinuationSpec {
  specId: string;
  goal: string;
  /** kept | recut | appended | dropped */
  disposition: string;
  note?: string | null;
}

export interface TicketApprovalPreview {
  project: string;
  ticketId: string;
  effect: TicketApprovalEffect;
  reason: string;
  executed: number;
  unstarted: number;
  basisRunId: string | null;
  specs: TicketContinuationSpec[];
  start?: { kind: TicketStartArm; runId?: string | null } | null;
}

/** Every 409 of POST /api/runs/{runId}/retry (2026-10-13-9a59n). */
export interface RetryRefusal {
  /** not-latest | handed-back | not-finished | not-infrastructure | closed-for-work | starting */
  reason: string;
  text: string;
  project: string;
  ticketId: string;
}

// 2026-10-13-9a59s: GET /api/runs/{runId}/compute (RunComputeRecordView, 9a59i + 9a59j) — every sandbox the
// run spawned with how it ended, the sandboxes no row counts, and each launch as a segment with its park.

/** exited | oom-killed | vanished | gone | released-at-park | released-at-requeue | released-at-end | unobserved */
export type SandboxEndReason = string;

export interface RunComputeSpawn {
  repo: string;
  spawnedAt: string | null;
  /** Null while open, and for an unobserved spawn (no close event reached it). */
  disposedAt: string | null;
  memory: string | null;
  endReason: SandboxEndReason | null;
  exitCode: number | null;
  oomKilled: boolean | null;
}

/** Who a parked segment waited on; kind null when neither a question nor a hand-back sits inside it. */
export interface RunComputePark {
  /** question | handback | null */
  kind: string | null;
  askedAt: string | null;
  questionId: string | null;
  /** A hand-back's answer is the cause of the ticket's next attempt. */
  answeredAt: string | null;
  answeredBy: string | null;
  resumedAt: string | null;
}

/** One launch of the run. endedAt null = open; endStatus "queued" is a capacity wait, never a park. */
export interface RunComputeSegment {
  startedAt: string;
  endedAt: string | null;
  endStatus: string | null;
  /** Summed call durations inside the segment — parallel sub-agents overlap, so it may exceed the span. */
  modelMs: number;
  park: RunComputePark | null;
}

export interface RunCompute {
  runId: string;
  spawns: RunComputeSpawn[];
  /** source-scope | browser | preflight — named, never counted. */
  uncounted: Array<{ kind: string; label: string }>;
  segments: RunComputeSegment[];
  unattributedCalls: number;
}
