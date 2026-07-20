// p0173a: TypeScript mirror of the C# SystemEvent hierarchy in
// AgentSmith.Contracts.Events. Hand-maintained — regenerate via
// `pnpm gen:hub-events` (the tool scans both RunEvent + SystemEvent).
//
// Slice a ships only the enum + base type. Slices b (poller + webhook)
// and c (chat + config + catalog) add their concrete record types
// alongside their producers; this file's `SystemEvent` union grows in
// lockstep.

export enum SystemEventType {
  // p0173b — poller + webhook instrumentation
  PollCycleStarted = 40,
  PollCycleFinished = 41,
  TicketScanned = 42,
  TicketSkipped = 43,
  TicketTriggered = 44,
  WebhookReceived = 45,

  // p0173c — channel + config + catalog instrumentation
  ChatMessageReceived = 50,
  ConfigFileRead = 51,
  SkillCatalogLoaded = 52,
  ConceptVocabularyLoaded = 53,

  // p0353 — config live-reload: pending -> applied
  ConfigChanged = 54,
  ConfigReloaded = 55,
}

export interface SystemEventBase {
  source: string;
  type: SystemEventType;
  timestamp: string;
}

// p0173b poller + webhook records.

export interface PollCycleStartedEvent extends SystemEventBase {
  type: SystemEventType.PollCycleStarted;
  tracker: string;
  intervalSeconds: number;
}

export interface PollCycleFinishedEvent extends SystemEventBase {
  type: SystemEventType.PollCycleFinished;
  tracker: string;
  ticketsPolled: number;
  matched: number;
  spawned: number;
  statusFiltered: number;
  zeroMatched: number;
  durationMs: number;
}

export interface TicketScannedEvent extends SystemEventBase {
  type: SystemEventType.TicketScanned;
  tracker: string;
  ticketId: string;
  labels: string[];
}

export enum TicketSkipReason {
  ZeroMatch = 0,
  StatusFilter = 1,
  ClaimBlocked = 2,
  DuplicateInFlight = 3,
  NotAnIssue = 4,
}

export interface TicketSkippedEvent extends SystemEventBase {
  type: SystemEventType.TicketSkipped;
  tracker: string;
  ticketId: string;
  reason: TicketSkipReason;
  detail: string;
}

export interface TicketTriggeredEvent extends SystemEventBase {
  type: SystemEventType.TicketTriggered;
  tracker: string;
  ticketId: string;
  project: string;
  pipeline: string;
  outcome: string;
}

export interface WebhookReceivedEvent extends SystemEventBase {
  type: SystemEventType.WebhookReceived;
  eventType: string;
  path: string;
  actioned: boolean;
  skipReason: string | null;
}

// p0173c chat + config + catalog records.

export interface ChatMessageReceivedEvent extends SystemEventBase {
  type: SystemEventType.ChatMessageReceived;
  channel: string;
  messageType: string;
  actioned: boolean;
  skipReason: string | null;
}

export enum ConfigFileKind {
  AgentSmithYml = 0,
  ContextYaml = 1,
  CodingPrinciplesMd = 2,
  SkillYaml = 3,
  ConceptVocabulary = 4,
}

export interface ConfigFileReadEvent extends SystemEventBase {
  type: SystemEventType.ConfigFileRead;
  path: string;
  kind: ConfigFileKind;
  sizeBytes: number;
  runId: string | null;
}

export interface SkillCatalogLoadedEvent extends SystemEventBase {
  type: SystemEventType.SkillCatalogLoaded;
  catalogVersion: string;
  skillsLoaded: number;
  skillsDropped: number;
  durationMs: number;
}

export interface ConceptVocabularyLoadedEvent extends SystemEventBase {
  type: SystemEventType.ConceptVocabularyLoaded;
  conceptCount: number;
  durationMs: number;
}

// p0353 config live-reload records.

export interface ConfigChangedEvent extends SystemEventBase {
  type: SystemEventType.ConfigChanged;
  epoch: number;
  actor: string;
}

export interface ConfigReloadedEvent extends SystemEventBase {
  type: SystemEventType.ConfigReloaded;
  epoch: number;
  trackerCount: number;
}

export type SystemEvent =
  | PollCycleStartedEvent
  | PollCycleFinishedEvent
  | TicketScannedEvent
  | TicketSkippedEvent
  | TicketTriggeredEvent
  | WebhookReceivedEvent
  | ChatMessageReceivedEvent
  | ConfigFileReadEvent
  | SkillCatalogLoadedEvent
  | ConceptVocabularyLoadedEvent
  | ConfigChangedEvent
  | ConfigReloadedEvent;
