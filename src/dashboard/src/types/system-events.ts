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

export type SystemEvent =
  | PollCycleStartedEvent
  | PollCycleFinishedEvent
  | TicketScannedEvent
  | TicketSkippedEvent
  | TicketTriggeredEvent
  | WebhookReceivedEvent
  | SystemEventBase; // slice c records will extend this union
