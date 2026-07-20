import type { SettingKey } from "@/lib/configApi";

// p0353: static metadata for the twelve global SETTINGS singletons — the taxonomy's
// editable singleton docs surfaced as one typed form each, mirroring the catalog's
// entity kinds. Order is the rail order (deployment-shaped things first, then the
// process knobs). `persistence` (bootstrap-only) and `secrets` (its own catalog
// kind) are deliberately absent.

export const SETTING_KEYS: SettingKey[] = [
  "orchestrator",
  "sandbox",
  "deployment",
  "registries",
  "primary_provider",
  "limits",
  "pipeline_cost_cap",
  "queue",
  "dialogue",
  "skills",
  "pipeline_storage",
  "pipeline_data_flow",
];

export const SETTING_LABEL: Record<SettingKey, string> = {
  orchestrator: "Orchestrator",
  sandbox: "Sandbox",
  deployment: "Deployment",
  registries: "Registries",
  primary_provider: "Primary provider",
  limits: "Limits",
  pipeline_cost_cap: "Pipeline cost cap",
  queue: "Queue",
  dialogue: "Dialogue",
  skills: "Skills",
  pipeline_storage: "Pipeline storage",
  pipeline_data_flow: "Pipeline data flow",
};

// The one-line subtitle under each settings title in the studio content area.
export const SETTING_SUBTITLE: Record<SettingKey, string> = {
  orchestrator: "orchestrator image pin and the run wall-time ceiling",
  sandbox: "sandbox agent image and per-step / per-command timeouts",
  deployment: "the single image pin feeding both orchestrator and sandbox when unset",
  registries: "private package feeds the agent authenticates against",
  primary_provider: "the default agent provider when a project names none",
  limits: "per-skill agentic loop ceilings — tool calls, tokens, sub-agents",
  pipeline_cost_cap: "USD / token budget per run — a default plus per-pipeline and per-tier caps",
  queue: "consumer backpressure and Redis retry cadence",
  dialogue: "hot-wait window and approval timeout for human dialogue",
  skills: "where the skill catalog is resolved from",
  pipeline_storage: "in-flight run-artifact store TTL",
  pipeline_data_flow: "data-flow gating — warn only, or enforce",
};

// The rail / header glyph per settings key.
export const SETTING_ICON: Record<SettingKey, string> = {
  orchestrator: "◇",
  sandbox: "▤",
  deployment: "⬡",
  registries: "◨",
  primary_provider: "✦",
  limits: "⚖",
  pipeline_cost_cap: "◍",
  queue: "≡",
  dialogue: "◊",
  skills: "✧",
  pipeline_storage: "⛁",
  pipeline_data_flow: "⇢",
};

export function isSettingKey(value: string | undefined): value is SettingKey {
  return !!value && (SETTING_KEYS as string[]).includes(value);
}
