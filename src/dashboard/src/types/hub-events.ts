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
  CatalogIssue = 30,
  SubAgentSpawned = 60,
  SubAgentObservation = 61,
  SubAgentFinding = 62,
  SubAgentFileWritten = 63,
  SubAgentToolCall = 64,
  SubAgentCompleted = 65,
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

export interface CatalogIssueEvent extends RunEventBase {
  type: EventType.CatalogIssue;
  severity: string;
  source: string;
  category: string;
  message: string;
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
  | CatalogIssueEvent
  | SubAgentSpawnedEvent
  | SubAgentObservationEvent
  | SubAgentFindingEvent
  | SubAgentFileWrittenEvent
  | SubAgentToolCallEvent
  | SubAgentCompletedEvent;

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
