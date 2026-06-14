// p0266: READ client for the resolved agent-smith config — "how the system is
// wired" (projects → repos/trackers/agent/pipelines + global defaults), the
// config-time complement to the per-run topology. The /api/config payload is a
// redacted allow-list: no secret is ever present on the wire, so these types
// carry only display-safe fields by construction.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

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
  host: string | null;
  defaultBranch: string | null;
}

export interface ConfigTracker {
  name: string;
  type: string;
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

export interface ConfigCostCap {
  usd: number;
  tokens: number;
}

export interface ConfigGlobals {
  sandbox: ConfigSandbox;
  orchestrator: ConfigOrchestrator;
  limits: ConfigLimits;
  costCap: ConfigCostCap;
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
