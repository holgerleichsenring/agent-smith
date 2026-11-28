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
  "pocs",
  "skills",
  "pipeline_data_flow",
  "trace",
];

export const SETTING_LABEL: Record<SettingKey, string> = {
  orchestrator: "Orchestrator",
  sandbox: "Sandbox",
  deployment: "Deployment",
  primary_provider: "Primary provider",
  limits: "Limits",
  pipeline_cost_cap: "Pipeline cost cap",
  queue: "Queue",
  dialogue: "Dialogue",
  spec_review: "Spec review",
  series: "Series",
  preconditions: "Preconditions",
  branch_cleanup: "Branch cleanup",
  tickets: "Ticket limits", // 2026-11-28-9bd1: was "Tickets", the ticket list's name too
  holds: "Holds",
  pocs: "PoCs",
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
  pocs: "config.setting.pocs",
  skills: "config.setting.skills",
  pipeline_data_flow: "config.setting.pipelineDataFlow",
  trace: "config.setting.trace",
} as const satisfies Record<SettingKey, TextKey>;

// The one-line subtitle under each settings title in the studio content area.
export const SETTING_SUBTITLE = {
  orchestrator: "config.setting.orchestrator.sub",
  sandbox: "config.setting.sandbox.sub",
  deployment: "config.setting.deployment.sub",
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
  pocs: "config.setting.pocs.sub",
  skills: "config.setting.skills.sub",
  pipeline_data_flow: "config.setting.pipelineDataFlow.sub",
  trace: "config.setting.trace.sub",
} as const satisfies Record<SettingKey, TextKey>;

// The rail / header glyph per settings key.
export const SETTING_ICON: Record<SettingKey, string> = {
  orchestrator: "◇",
  sandbox: "▤",
  deployment: "⬡",
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
  pocs: "▦",
  skills: "✧",
  pipeline_data_flow: "⇢",
  trace: "⌁",
};

export function isSettingKey(value: string | undefined): value is SettingKey {
  return !!value && (SETTING_KEYS as string[]).includes(value);
}

// 2026-11-28-9bd1: the rail lists GROUPS of the small limit and cadence pages, beside the pages that
// stay their own. A group page holds its members as sections; a member's old path redirects to it.
// A group's slug is never a settings key, so /config/settings/{slug} names one or the other.

export type SettingGroupId = "runs" | "workflow" | "diagnostics";

export interface SettingGroup {
  id: SettingGroupId;
  label: TextKey;
  /** The English name, for the header's place table, which reads strings. */
  name: string;
  subtitle: TextKey;
  icon: string;
  members: SettingKey[];
}

export const SETTING_GROUPS: SettingGroup[] = [
  {
    id: "runs",
    label: "config.settingGroup.runs",
    name: "Runs & limits",
    subtitle: "config.settingGroup.runs.sub",
    icon: "⚖",
    members: ["orchestrator", "primary_provider", "limits", "pipeline_cost_cap", "queue", "deployment"],
  },
  {
    id: "workflow",
    label: "config.settingGroup.workflow",
    name: "Workflow",
    subtitle: "config.settingGroup.workflow.sub",
    icon: "◊",
    members: ["dialogue", "spec_review", "preconditions", "holds", "branch_cleanup"],
  },
  {
    id: "diagnostics",
    label: "config.settingGroup.diagnostics",
    name: "Diagnostics",
    subtitle: "config.settingGroup.diagnostics.sub",
    icon: "⌁",
    members: ["pipeline_data_flow", "trace"],
  },
];

/** One rail entry: a group of sections, or a settings page of its own. */
export type SettingsNavEntry = { kind: "group"; group: SettingGroup } | { kind: "setting"; key: SettingKey };

export function isSettingGroup(value: string | undefined): value is SettingGroupId {
  return SETTING_GROUPS.some((g) => g.id === value);
}

export function settingGroup(id: SettingGroupId): SettingGroup {
  return SETTING_GROUPS.find((g) => g.id === id) ?? SETTING_GROUPS[0];
}

const group = (id: SettingGroupId): SettingsNavEntry => ({ kind: "group", group: settingGroup(id) });
const page = (key: SettingKey): SettingsNavEntry => ({ kind: "setting", key });

/** The rail order: the run-shaped knobs, the workflow cadences, the pages of their own, diagnostics last. */
export const SETTINGS_NAV: SettingsNavEntry[] = [
  group("runs"),
  group("workflow"),
  page("sandbox"),
  page("series"),
  page("tickets"),
  page("pocs"),
  page("skills"),
  group("diagnostics"),
];

export function settingsNavHref(entry: SettingsNavEntry): string {
  return `/config/settings/${entry.kind === "group" ? entry.group.id : entry.key}`;
}

export function settingsNavLabel(entry: SettingsNavEntry): TextKey {
  return entry.kind === "group" ? entry.group.label : SETTING_LABEL_KEY[entry.key];
}

export function settingsNavName(entry: SettingsNavEntry): string {
  return entry.kind === "group" ? entry.group.name : SETTING_LABEL[entry.key];
}

export function settingsNavIcon(entry: SettingsNavEntry): string {
  return entry.kind === "group" ? entry.group.icon : SETTING_ICON[entry.key];
}

/** The group a settings key is a section of, or null when it is a page of its own. */
export function groupOf(key: SettingKey): SettingGroup | null {
  return SETTING_GROUPS.find((g) => g.members.includes(key)) ?? null;
}

/** Where a former settings page lives now: its group with its section open, or its own page. */
export function settingHref(key: SettingKey): string {
  const owner = groupOf(key);
  return owner ? `/config/settings/${owner.id}?open=${key}` : `/config/settings/${key}`;
}

/**
 * How many values of a saved doc differ from its default. Every leaf that differs counts once, and
 * an array counts once as a whole — a list is one value to the operator. A sub-object one side lacks
 * counts its leaves (a cost-cap tier the doc leaves out is two values: usd and tokens).
 */
export function changedCount(value: unknown, defaults: unknown): number {
  if (isRecord(value) && isRecord(defaults)) {
    const keys = new Set([...Object.keys(value), ...Object.keys(defaults)]);
    let count = 0;
    for (const key of keys) count += changedCount(value[key], defaults[key]);
    return count;
  }
  if (same(value, defaults)) return 0;
  if (isRecord(value)) return Math.max(1, leafCount(value));
  if (isRecord(defaults)) return Math.max(1, leafCount(defaults));
  return 1;
}

function leafCount(record: Record<string, unknown>): number {
  return Object.values(record).reduce<number>((n, v) => n + (isRecord(v) ? leafCount(v) : 1), 0);
}

function same(a: unknown, b: unknown): boolean {
  return JSON.stringify(a ?? null) === JSON.stringify(b ?? null);
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return typeof v === "object" && v !== null && !Array.isArray(v);
}
