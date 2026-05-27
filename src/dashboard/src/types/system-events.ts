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

// Slice a: union has no concrete members yet — slices b + c add them.
// Until then, components consuming SystemEvent receive the base shape.
export type SystemEvent = SystemEventBase;
