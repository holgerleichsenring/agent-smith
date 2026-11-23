import type { SettingKey } from "@/lib/configApi";
import type { TextKey } from "@/lib/i18n/translate";

// 2026-08-26-7a51: `role_mapping` left this rail for the Access surface, which needs
// access.write rather than config.write — a custom role bundling config.write is legal,
// and granting a role is how such a caller would make themselves an administrator.
// p0353: static metadata for the global SETTINGS singletons — the taxonomy's
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
  "spec_review",
  "series",
  "preconditions",
  "branch_cleanup",
  "tickets",
  "holds",
  "skills",
  "pipeline_data_flow",
  "trace",
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
  spec_review: "Spec review",
  series: "Series",
  preconditions: "Preconditions",
  branch_cleanup: "Branch cleanup",
  tickets: "Tickets",
  holds: "Holds",
  skills: "Skills",
  pipeline_data_flow: "Pipeline data flow",
  trace: "Trace",
};

// 2026-11-23-5669d: the same names as dictionary keys, for what the studio renders. SETTING_LABEL stays for the
// header's place table, which reads strings.
export const SETTING_LABEL_KEY = {
  orchestrator: "config.setting.orchestrator",
  sandbox: "config.setting.sandbox",
  deployment: "config.setting.deployment",
  registries: "config.setting.registries",
  primary_provider: "config.setting.primaryProvider",
  limits: "config.setting.limits",
  pipeline_cost_cap: "config.setting.pipelineCostCap",
  queue: "config.setting.queue",
  dialogue: "config.setting.dialogue",
  spec_review: "config.setting.specReview",
  series: "config.setting.series",
  preconditions: "config.setting.preconditions",
  branch_cleanup: "config.setting.branchCleanup",
  tickets: "config.setting.tickets",
  holds: "config.setting.holds",
  skills: "config.setting.skills",
  pipeline_data_flow: "config.setting.pipelineDataFlow",
  trace: "config.setting.trace",
} as const satisfies Record<SettingKey, TextKey>;

// The one-line subtitle under each settings title in the studio content area.
export const SETTING_SUBTITLE = {
  orchestrator: "config.setting.orchestrator.sub",
  sandbox: "config.setting.sandbox.sub",
  deployment: "config.setting.deployment.sub",
  registries: "config.setting.registries.sub",
  primary_provider: "config.setting.primaryProvider.sub",
  limits: "config.setting.limits.sub",
  pipeline_cost_cap: "config.setting.pipelineCostCap.sub",
  queue: "config.setting.queue.sub",
  dialogue: "config.setting.dialogue.sub",
  spec_review: "config.setting.specReview.sub",
  series: "config.setting.series.sub",
  preconditions: "config.setting.preconditions.sub",
  branch_cleanup: "config.setting.branchCleanup.sub",
  tickets: "config.setting.tickets.sub",
  holds: "config.setting.holds.sub",
  skills: "config.setting.skills.sub",
  pipeline_data_flow: "config.setting.pipelineDataFlow.sub",
  trace: "config.setting.trace.sub",
} as const satisfies Record<SettingKey, TextKey>;

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
  spec_review: "⚑",
  series: "⋯",
  preconditions: "⧗",
  branch_cleanup: "⌫",
  tickets: "⊞",
  holds: "⚠",
  skills: "✧",
  pipeline_data_flow: "⇢",
  trace: "⌁",
};

export function isSettingKey(value: string | undefined): value is SettingKey {
  return !!value && (SETTING_KEYS as string[]).includes(value);
}
