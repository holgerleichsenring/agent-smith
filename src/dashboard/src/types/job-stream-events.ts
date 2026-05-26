// p0169b: typed event shapes consumed by useJobStream + LiveLogPanel.
// Mirrors the backend's SseEventWriter event-name vocabulary.

export type ProgressEvent = {
  type: "progress";
  step: number;
  total: number;
  command_name: string;
};

export type ToolCallEvent = {
  type: "tool_call";
  tool_name: string;
  args_preview: string;
};

export type SkillObservationEvent = {
  type: "skill_observation";
  severity: string;
  category: string;
  body_preview: string;
  source_ref: string;
};

export type DoneEvent = {
  type: "done";
  run_id: string;
  summary: string;
  pr_url: string | null;
};

export type ErrorEvent = {
  type: "error";
  run_id: string;
  error_context: string;
};

export type JobStreamEvent =
  | ProgressEvent
  | ToolCallEvent
  | SkillObservationEvent
  | DoneEvent
  | ErrorEvent;

export type JobStreamStatus = "idle" | "connecting" | "open" | "reconnecting" | "closed";
