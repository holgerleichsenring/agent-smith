// p0169f: TypeScript mirror of the C# RunEvent record family in
// AgentSmith.Contracts.Events. Hand-maintained — regenerate via
// `pnpm gen:hub-events` after editing the C# records.

export enum EventType {
  RunStarted = 0,
  RunFinished = 1,
  SandboxCreated = 2,
  SandboxDisposed = 3,
  StepStarted = 4,
  StepFinished = 5,
  DecisionLogged = 10,
  GateChecked = 11,
  TriageRoute = 12,
  LlmCallStarted = 13,
  LlmCallFinished = 14,
  SandboxCommand = 20,
  SandboxOutput = 21,
  SandboxResult = 22,
  ToolCall = 23,
  ToolResult = 24,
  L1StepDetail = 25,
  TicketFetched = 26,
  CatalogLoaded = 27,
  PullRequestOutcome = 28,
  CatalogIssue = 30,
  TicketInstructionIgnored = 31,
  SubAgentSpawned = 60,
  SubAgentObservation = 61,
  SubAgentFinding = 62,
  SubAgentFileWritten = 63,
  SubAgentToolCall = 64,
  SubAgentCompleted = 65,
  RunCancelRequested = 70,
  SandboxVanished = 71,
  RunCheckpointed = 72,
  ExpectationRatified = 73,
  RunStoryRecorded = 74,
}

interface RunEventBase {
  runId: string;
  type: EventType;
  timestamp: string;
}

export interface RunStartedEvent extends RunEventBase {
  type: EventType.RunStarted;
  trigger: string;
  pipeline: string;
  repos: string[];
  startedAt: string;
  /** p0186: agent display label ("type/model"). Null for pre-p0186 events. */
  agentName: string | null;
  /** p0275: the pipeline's known ordered step labels. The execution tree seeds
   *  its step rail from this so early steps survive event-buffer eviction.
   *  Absent on pre-p0275 events → event-only step list. */
  plannedSteps?: string[] | null;
  /** p0320c: resolved project name + tracker platform, stamped onto the run row
   *  for the capacity-queue TOCTOU backstop. Absent on pre-p0320c events. */
  project?: string | null;
  platform?: string | null;
}

export interface RunFinishedEvent extends RunEventBase {
  type: EventType.RunFinished;
  status: string;
  prUrl: string | null;
  summary: string;
  finishedAt: string;
  /** p0176b: pipeline-aggregate cost from PipelineCostTracker at run end. */
  costUsd: number | null;
}

export interface SandboxCreatedEvent extends RunEventBase {
  type: EventType.SandboxCreated;
  repo: string;
  image: string;
  language: string | null;
  /** p0332: the pod's Kubernetes memory-request quantity (e.g. "1Gi").
   *  Additive trailing optional — null/absent on pre-p0332 events. */
  memoryRequest?: string | null;
}

export interface SandboxDisposedEvent extends RunEventBase {
  type: EventType.SandboxDisposed;
  repo: string;
  exitCode: number | null;
}

export interface StepStartedEvent extends RunEventBase {
  type: EventType.StepStarted;
  stepIndex: number;
  stepName: string;
  totalSteps: number;
  /** p0203: operator-facing label from CommandDisplayNames; null for
   *  pre-p0203 producers — consumers fall back to stepName. */
  displayName: string | null;
}

export interface StepFinishedEvent extends RunEventBase {
  type: EventType.StepFinished;
  stepIndex: number;
  status: string;
  durationMs: number;
  reason: string | null;
}

export interface DecisionLoggedEvent extends RunEventBase {
  type: EventType.DecisionLogged;
  category: string;
  chose: string;
  over: string | null;
  reason: string;
}

export interface GateCheckedEvent extends RunEventBase {
  type: EventType.GateChecked;
  gate: string;
  passed: boolean;
  reason: string;
}

export interface TriageRouteEvent extends RunEventBase {
  type: EventType.TriageRoute;
  skill: string;
  role: string;
  confidence: number;
}

export interface LlmCallStartedEvent extends RunEventBase {
  type: EventType.LlmCallStarted;
  model: string;
  role: string;
  promptHash: string;
  /** p0176a: phase (string form of SkillExecutionPhase) from the ambient CallScope. */
  phase: string | null;
  /** p0176a: repo name from the ambient CallScope on multi-repo runs. */
  repoName: string | null;
}

export interface LlmCallFinishedEvent extends RunEventBase {
  type: EventType.LlmCallFinished;
  model: string;
  role: string;
  tokensIn: number;
  tokensOut: number;
  costUsd: number;
  durationMs: number;
  /** p0176a: phase (string form of SkillExecutionPhase) from the ambient CallScope. */
  phase: string | null;
  /** p0176a: repo name from the ambient CallScope on multi-repo runs. */
  repoName: string | null;
  /** p0323: prompt tokens served from the provider cache. Optional — events
   *  persisted before p0323 lack the field; treat undefined as 0. */
  cachedTokensIn?: number;
  /** p0323: prompt tokens written to the provider cache this call. */
  cacheCreationTokensIn?: number;
}

export interface SandboxCommandEvent extends RunEventBase {
  type: EventType.SandboxCommand;
  repo: string;
  command: string;
  argsLength: number;
  /** p0175-fix: producer-curated one-liner (≤120 chars), null when unsafe to surface. */
  summary: string | null;
}

export interface SandboxOutputEvent extends RunEventBase {
  type: EventType.SandboxOutput;
  repo: string;
  stream: "stdout" | "stderr";
  line: string;
  batchSeq: number;
}

export interface SandboxResultEvent extends RunEventBase {
  type: EventType.SandboxResult;
  repo: string;
  command: string;
  exitCode: number;
  durationMs: number;
}

export interface ToolCallEvent extends RunEventBase {
  type: EventType.ToolCall;
  tool: string;
  argsLength: number;
  /** p0175-fix: producer-curated one-liner (≤120 chars), null when unsafe to surface. */
  summary: string | null;
  /** p0222: the agent's one-sentence intent narration for the turn this tool call belongs to. */
  intent: string | null;
  /** p0176a: role from the ambient CallScope so per-skill tool activity is attributable. */
  role: string | null;
  /** p0176a: phase (string form of SkillExecutionPhase) from the ambient CallScope. */
  phase: string | null;
  /** p0176a: repo name from the ambient CallScope on multi-repo runs. */
  repoName: string | null;
}

export interface ToolResultEvent extends RunEventBase {
  type: EventType.ToolResult;
  tool: string;
  ok: boolean;
  resultLength: number;
  errorMessage: string | null;
  /** p0176a: role from the ambient CallScope so per-skill tool activity is attributable. */
  role: string | null;
  /** p0176a: phase (string form of SkillExecutionPhase) from the ambient CallScope. */
  phase: string | null;
  /** p0176a: repo name from the ambient CallScope on multi-repo runs. */
  repoName: string | null;
}

export interface L1StepDetailEvent extends RunEventBase {
  type: EventType.L1StepDetail;
  stepIndex: number;
  origin: string;
  detail: string;
}

export interface TicketFetchedEvent extends RunEventBase {
  type: EventType.TicketFetched;
  ticketId: string;
  title: string;
  description: string;
  state: string;
  labels: string[];
  attachmentCount: number;
  source: string;
}

/**
 * p0205: per-run catalog binding emitted by the visible Load-catalog step —
 * what THIS run resolved the skill catalog to. Mirrors C# CatalogLoadedEvent.
 */
export interface CatalogLoadedEvent extends RunEventBase {
  type: EventType.CatalogLoaded;
  version: string;
  source: string;
  sourceUrl: string;
  conceptCount: number;
  skillsLoaded: number;
  mastersCount: number;
  fromCache: boolean;
  durationMs: number;
  // p0210: alphabetically-sorted catalog inventories this run bound to.
  // Absent on runs that landed before p0210 — consumers fall back to counts.
  skillNames?: string[];
  masterNames?: string[];
  conceptNames?: string[];
}

export interface CatalogIssueEvent extends RunEventBase {
  type: EventType.CatalogIssue;
  severity: string;
  source: string;
  category: string;
  message: string;
}

/**
 * p0223: per-repo outcome of the commit/PR step. status is
 * "opened" | "no_changes" | "failed"; url carries the created PR when opened;
 * reason carries the real failure reason when failed.
 */
export interface PullRequestOutcomeEvent extends RunEventBase {
  type: EventType.PullRequestOutcome;
  repo: string;
  status: "opened" | "no_changes" | "failed";
  url: string | null;
  reason: string | null;
}

/**
 * p0316: the master refused a ticket-embedded instruction (out-of-scope /
 * destructive / prompt-injection). One event per ignored instruction.
 */
export interface TicketInstructionIgnoredEvent extends RunEventBase {
  type: EventType.TicketInstructionIgnored;
  quote: string;
  reason: string;
}

export interface SubAgentSpawnedEvent extends RunEventBase {
  type: EventType.SubAgentSpawned;
  subAgentId: string;
  name: string;
  activity: string;
  parentSubAgentId: string | null;
  inheritedContextHash: string;
}

export interface SubAgentObservationEvent extends RunEventBase {
  type: EventType.SubAgentObservation;
  subAgentId: string;
  text: string;
}

export interface SubAgentFindingEvent extends RunEventBase {
  type: EventType.SubAgentFinding;
  subAgentId: string;
  severity: string;
  title: string;
  detail: string;
}

export interface SubAgentFileWrittenEvent extends RunEventBase {
  type: EventType.SubAgentFileWritten;
  subAgentId: string;
  path: string;
  bytes: number;
}

export interface SubAgentToolCallEvent extends RunEventBase {
  type: EventType.SubAgentToolCall;
  subAgentId: string;
  toolName: string;
  argsSummary: string | null;
}

export interface SubAgentCompletedEvent extends RunEventBase {
  type: EventType.SubAgentCompleted;
  subAgentId: string;
  status: string;
  observationsCount: number;
  findingsCount: number;
  filesWrittenCount: number;
  toolCalls: number;
  costUsd: number;
}

/**
 * p0200: emitted when an operator (POST /api/runs/{runId}/cancel) or the
 * PipelineRunWatchdog signals a cancel. RunSnapshot.cancelRequested flips
 * true so the dashboard card can render "cancelling…" until the terminal
 * RunFinished lands.
 */
export interface RunCancelRequestedEvent extends RunEventBase {
  type: EventType.RunCancelRequested;
  reason: string;
  requestedAt: string;
}

/**
 * p0201: emitted by SandboxLivenessWatcher when the sandbox container is
 * confirmed gone (heartbeat-missing > 3 ticks + docker-inspect verdict !=
 * Running). containerState distinguishes "Exited(137)" from "Gone".
 */
export interface SandboxVanishedEvent extends RunEventBase {
  type: EventType.SandboxVanished;
  jobId: string;
  repo: string;
  lastHeartbeatAt: string | null;
  reason: string;
  containerState: string;
}

/**
 * p0327: emitted when a run parks on a DialogQuestion past the hot-wait
 * threshold. Carries the serialized checkpoint for the server-side projector;
 * the dashboard reads the result via RunSnapshot (status waiting_for_input +
 * pendingQuestion), not from this event directly.
 */
export interface RunCheckpointedEvent extends RunEventBase {
  type: EventType.RunCheckpointed;
  project: string;
  ticketId: string;
  platform: string | null;
  pipeline: string;
  dialogueJobId: string;
  questionId: string;
  questionJson: string;
  remainingCommandsJson: string;
  contextJson: string;
  executionCount: number;
  askedAt: string;
  answerDeadlineAt: string;
}

/**
 * p0328: the ratification outcome of the run's expectation negotiation.
 * DraftJson/RatifiedJson are serialized ExpectationDraft payloads; the
 * server-side applier persists this as the RunExpectation row.
 */
export interface ExpectationRatifiedEvent extends RunEventBase {
  type: EventType.ExpectationRatified;
  draftJson: string;
  ratifiedJson: string;
  outcome: string;
  ratifiedBy: string;
  editDistance: number;
}

/**
 * p0344b: emitted by WriteRunResult with the run's story artifacts — the
 * progress ledger and the acceptance dispositions as serialized JSON. The
 * server-side projector persists them onto the run row; the dashboard reads
 * the result via RunSnapshot.progressLedger/acceptance (REST detail), not
 * from this event directly.
 */
export interface RunStoryRecordedEvent extends RunEventBase {
  type: EventType.RunStoryRecorded;
  progressLedgerJson: string | null;
  acceptanceJson: string | null;
}

export type RunEvent =
  | RunStartedEvent
  | RunFinishedEvent
  | SandboxCreatedEvent
  | SandboxDisposedEvent
  | StepStartedEvent
  | StepFinishedEvent
  | DecisionLoggedEvent
  | GateCheckedEvent
  | TriageRouteEvent
  | LlmCallStartedEvent
  | LlmCallFinishedEvent
  | SandboxCommandEvent
  | SandboxOutputEvent
  | SandboxResultEvent
  | ToolCallEvent
  | ToolResultEvent
  | L1StepDetailEvent
  | TicketFetchedEvent
  | CatalogLoadedEvent
  | CatalogIssueEvent
  | PullRequestOutcomeEvent
  | TicketInstructionIgnoredEvent
  | SubAgentSpawnedEvent
  | SubAgentObservationEvent
  | SubAgentFindingEvent
  | SubAgentFileWrittenEvent
  | SubAgentToolCallEvent
  | SubAgentCompletedEvent
  | RunCancelRequestedEvent
  | SandboxVanishedEvent
  | RunCheckpointedEvent
  | ExpectationRatifiedEvent
  | RunStoryRecordedEvent;

/** p0327: the pending question of a status="waiting_for_input" run, joined
 *  from its checkpoint row at query time (REST detail only). */
export interface PendingQuestionInfo {
  questionId: string;
  type: string;
  text: string;
  context: string | null;
  choices: string[];
  defaultAnswer: string | null;
  askedAt: string;
  answerDeadlineAt: string;
}

/** p0344b: server-computed state of one story beat. Derived from the typed
 *  pipeline commands on the backend — never guessed from step labels. */
export type BeatState = "done" | "active" | "pending" | "failed" | "skipped";

/** p0344b: the five story beats, each with its server-computed state. */
export interface RunBeats {
  ticket: BeatState;
  plan: BeatState;
  building: BeatState;
  verify: BeatState;
  outcome: BeatState;
}

/** p0344b: one persisted progress-ledger row (p0341), served on the detail. */
export interface ProgressLedgerEntry {
  id: string;
  activity: string;
  status: "pending" | "in_progress" | "done";
  target: string | null;
}

/** p0344b: one acceptance criterion with its keystone disposition. */
export interface AcceptanceCriterion {
  text: string;
  status: "met" | "unmet" | "not_applicable" | "unproven";
  reason: string | null;
}

/** p0344b: the run's persisted acceptance dispositions (p0340 keystone). */
export interface RunAcceptance {
  criteria: AcceptanceCriterion[];
  outcome: string | null;
  ratifiedBy: string | null;
}

export interface RunSnapshot {
  runId: string;
  pipeline: string;
  trigger: string;
  repos: string[];
  status: string;
  prUrl: string | null;
  summary: string | null;
  startedAt: string;
  finishedAt: string | null;
  sandboxes: number;
  stepIndex: number;
  stepName: string | null;
  totalSteps: number;
  lastEventType: string | null;
  /** p0175-fix: rolled-up LLM cost from LlmCallFinished events. */
  costUsd: number;
  llmCalls: number;
  /** p0184: ticket id + human-readable title surfaced by TicketFetchedEvent.
   *  Null until the FetchTicket step lands on the stream. */
  ticketId: string | null;
  ticketTitle: string | null;
  /** p0186: agent display label ("type/model"). Null for pre-p0186 runs. */
  agentName: string | null;
  /** p0200: flipped true by RunCancelRequestedEvent. RunCard renders
   *  "cancelling…" until the terminal RunFinished lands. */
  cancelRequested: boolean;
  /** p0320d: 1-based FIFO position for a status="queued" run, computed at query
   *  time from the capacity queue. Absent/null for non-queued runs and on the
   *  live SignalR path. */
  queuePosition?: number | null;
  /** p0332: RESERVED capacity-time for a finished run — memory request × pod
   *  lifetime in Gi·minutes, summed over sandboxes + the spawned orchestrator.
   *  Reservation, NOT measured consumption and NOT money. Null while running,
   *  on pre-p0332 rows, and on the live SignalR path. */
  reservedGiMinutes?: number | null;
  /** p0327: the parked run's pending DialogQuestion for status
   *  "waiting_for_input". Present on the REST detail path only. */
  pendingQuestion?: PendingQuestionInfo | null;
  /** p0336: the run's capacity calculation — pods (each with its resolved limit),
   *  totals, dropped repos/contexts, the human reason, and whether the run holds
   *  a budget reservation. Joined from the ledger on the REST path. */
  footprint?: RunFootprintView | null;
  /** p0348: the pods the run ACTUALLY spawned (from the persisted RunSandbox
   *  rows) — the honest "live compute" the side rail shows, distinct from the
   *  over-counting reservation in `footprint`. Null/absent until the first
   *  sandbox lands (client renders "calculating…") and on the live SignalR path.
   *  Persists after the run because the rows do. */
  liveCompute?: RunComputeView | null;
  /** p0344b: server-computed beat states (list + detail). Null/absent on runs
   *  persisted before the beats existed — the client renders NO storybar then,
   *  never a guess. */
  beats?: RunBeats | null;
  /** p0344b: the persisted p0341 progress ledger (detail only). Null/absent on
   *  pre-migration rows. */
  progressLedger?: ProgressLedgerEntry[] | null;
  /** p0344b: persisted per-criterion acceptance dispositions (detail only).
   *  Null/absent on pre-migration rows — the client falls back to the
   *  ExpectationRatified event, or an honest empty state. */
  acceptance?: RunAcceptance | null;
  /** p0350: EVERY pull request the run opened (one per repo). `prUrl` above is
   *  the first opened PR for back-compat; this carries all of them so a
   *  multi-repo run shows each. Empty/absent when no PR was opened. */
  pullRequests?: RunPullRequest[] | null;
}

/** p0350: one pull request a run opened, per repo. */
export interface RunPullRequest {
  repo: string;
  url: string;
  status: string;
  isDraft: boolean;
}

/** p0347: the status of one per-repo pull-request attempt the agent recorded at
 *  open time. "opened" = a PR exists (url set); "no_changes" = the run produced
 *  nothing to open; "failed" = the open itself failed (reason set). Mirrors the
 *  C# PullRequestOutcomeEvent.status. */
export type PullRequestStatus = "opened" | "no_changes" | "failed";

/** p0347: one row of GET /api/pull-requests — a per-repo PR outcome flattened
 *  across runs and joined with its run/ticket facts, newest-first. */
export interface PullRequest {
  runId: string;
  ticketId: string | null;
  ticketTitle: string | null;
  pipeline: string;
  repo: string;
  status: PullRequestStatus;
  url: string | null;
  reason: string | null;
  openedAt: string;
}

/** p0336: one pod in a run's computed footprint. */
export interface RunFootprintPod {
  repo: string;
  contexts: string[];
  image: string;
  cpuLimit: string;
  memLimit: string;
}

/** p0336: a repo/context scoping dropped from the footprint, with why. */
export interface DroppedContext {
  repo: string;
  context: string | null;
  reason: string;
}

/** p0336: the run's capacity calculation for the footprint panel. */
export interface RunFootprintView {
  pods: RunFootprintPod[];
  totalCpuLimit: string;
  totalMemLimit: string;
  dropped: DroppedContext[];
  reason: string;
  reserved: boolean;
}

/** p0348: one pod the run actually spawned (a persisted RunSandbox row). */
export interface RunComputePod {
  repo: string;
  image: string;
  mem: string;
  status: string;
}

/** p0348: the pods a run actually spawned — the honest live-compute view. */
export interface RunComputeView {
  pods: RunComputePod[];
  totalMem: string;
}

export interface OverviewSnapshot {
  active: RunSnapshot[];
  recent: RunSnapshot[];
  /**
   * p0175-fix: server-computed 24h rollup. Null when the snapshot was
   * produced before the field existed (defensive — should always be
   * present from a current backend).
   */
  systemActivity: SystemActivitySnapshot | null;
}

/**
 * p0175-fix: server-truth 24h KPI rollup pushed alongside the overview
 * snapshot + live via SystemActivityUpdated. Replaces the
 * client-derived useActivityKpis + useChannelBreakdown which drifted
 * once the 500-event client buffer overflowed.
 */
export interface SystemActivitySnapshot {
  ticketsScanned: number;
  ticketsTriggered: number;
  ticketsSkipped: number;
  webhooksReceived: number;
  webhooksActioned: number;
  pollCyclesStarted: number;
  pollCyclesFinished: number;
  eventsPerSource: Record<string, number>;
}
