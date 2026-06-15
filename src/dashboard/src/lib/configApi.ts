// p0266/p0270: READ client for the resolved agent-smith config. The /api/config
// payload is a redacted allow-list (no secret on the wire). p0270a enriches each
// project with its RESOLVED effective settings + provenance (where the value
// comes from) and the tracker trigger semantics — so the dashboard renders
// exactly what the runtime resolves, not a second computation.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

// Where an effective value came from. "run-resolved" = not knowable at config
// time (e.g. the toolchain image is chosen per run from the repo's context.yaml).
export type ResolutionSource = "global-default" | "override" | "run-resolved";

export interface ResolvedValue<T> {
  value: T | null;
  source: ResolutionSource;
}

export interface ResourceSummary {
  cpuRequest: string;
  cpuLimit: string;
  memoryRequest: string;
  memoryLimit: string;
}

export interface CostCap {
  usd: number;
  tokens: number;
}

export interface ResolvedSettings {
  stepTimeoutSeconds: ResolvedValue<number>;
  runCommandTimeoutSeconds: ResolvedValue<number>;
  sandboxResources: ResolvedValue<ResourceSummary>;
  agentImage: ResolvedValue<string>;
  orchestratorImage: ResolvedValue<string>;
  toolchainImage: ResolvedValue<string>;
  costCap: ResolvedValue<CostCap>;
  resolutionError: string | null;
}

export interface TriggerSemantics {
  triggerStatuses: string[];
  doneStatus: string | null;
  failedStatus: string | null;
  pollingEnabled: boolean;
  pollingIntervalSeconds: number;
  commentKeyword: string | null;
}

export interface ConfigAgent {
  name: string;
  type: string;
  model: string;
  networkTimeoutSeconds: number;
  maxFixIterations: number;
  requestsPerMinute: number | null;
  inputTokensPerMinute: number | null;
  maxConcurrentSkillRounds: number;
}

export interface ConfigRepo {
  name: string;
  type: string;
  url: string | null;
  organization: string | null;
  project: string | null;
  defaultBranch: string | null;
}

export interface ConfigTracker {
  name: string;
  type: string;
  url: string | null;
  project: string | null;
  openStates: string[];
  doneStatus: string | null;
}

export interface ConfigProject {
  name: string;
  pipeline: string;
  agentName: string;
  trackerName: string;
  repoNames: string[];
  pipelines: string[];
  resolved: ResolvedSettings;
  trigger: TriggerSemantics;
}

export type ConfigEdgeKind = "repo" | "tracker" | "agent" | "pipeline";

export interface ConfigEdge {
  from: string;
  to: string;
  kind: ConfigEdgeKind;
}

export interface ConfigSandbox {
  agentRegistry: string;
  agentVersion: string;
  stepTimeoutSeconds: number;
  runCommandTimeoutSeconds: number;
}

export interface ConfigOrchestrator {
  registry: string;
  version: string;
  maxRunWallTimeSeconds: number;
}

export interface ConfigLimits {
  maxToolCallsPerSkill: number;
  maxLlmCallsPerSkill: number;
  maxConcurrentSkillCalls: number;
  maxSubAgentsPerRun: number;
}

export interface ConfigGlobals {
  sandbox: ConfigSandbox;
  orchestrator: ConfigOrchestrator;
  limits: ConfigLimits;
  costCap: CostCap;
  persistenceProvider: string;
}

export interface ConfigSnapshot {
  agents: ConfigAgent[];
  repos: ConfigRepo[];
  trackers: ConfigTracker[];
  projects: ConfigProject[];
  edges: ConfigEdge[];
  globals: ConfigGlobals;
}

export async function fetchConfig(signal?: AbortSignal): Promise<ConfigSnapshot> {
  const res = await fetch(`${API_BASE}/api/config`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as ConfigSnapshot;
}
