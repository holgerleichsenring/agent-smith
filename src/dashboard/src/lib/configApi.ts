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

// ---------------------------------------------------------------------------
// p0345: Config Studio — the DB-backed EDITABLE catalog. Distinct from the
// read-only resolved snapshot above: this is the CRUD surface the studio forms
// write against. Refs (agent/tracker/repos, and every *Secret) are catalog IDs,
// never free text — the forms pick them and the backend enforces the same FKs,
// so a broken reference is structurally impossible. Secrets carry env-NAMES
// only; a secret value never crosses this client.
// ---------------------------------------------------------------------------

export type ConfigEntityKind =
  | "agents"
  | "trackers"
  | "repos"
  | "projects"
  | "mcp-servers"
  | "secrets";

export interface AgentModels {
  coding: string;
  scan: string;
}

/** provider + the two model roles + a FK to the secret holding the API key. */
export interface StudioAgent {
  id: string;
  provider: string;
  models: AgentModels;
  keySecret: string;
}

export interface StudioTracker {
  id: string;
  type: string;
  org: string;
  project: string;
  authSecret: string;
}

export interface StudioRepo {
  id: string;
  name: string;
  branch: string;
}

/** The relational heart: agent + tracker are single FKs, repos a FK set. */
export interface StudioProject {
  id: string;
  agent: string;
  tracker: string;
  repos: string[];
  trigger: string;
  pipelines: string[];
}

export interface StudioMcpServer {
  id: string;
  transport: string;
  url: string;
  authSecret: string;
}

/** A secret is nothing but its env-NAME. No value field exists, by design. */
export interface StudioSecret {
  id: string;
}

/** Union of every editable entity, keyed only by the shared `id`. */
export type StudioEntity =
  | StudioAgent
  | StudioTracker
  | StudioRepo
  | StudioProject
  | StudioMcpServer
  | StudioSecret;

export type ConfigChangeAction = "create" | "update" | "delete" | "revert";

export interface ConfigChangeField {
  field: string;
  before: string | null;
  after: string | null;
}

/** One attributed, revertible audit row — who/when/what-diff. */
export interface ConfigChange {
  id: string;
  actor: string;
  timestampUtc: string;
  entityKind: ConfigEntityKind;
  entityId: string;
  action: ConfigChangeAction;
  fields: ConfigChangeField[];
  reverted: boolean;
}

async function readJson<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as T;
}

/** A typed CRUD client bound to one entity endpoint. */
export interface CrudClient<T extends { id: string }> {
  kind: ConfigEntityKind;
  list(signal?: AbortSignal): Promise<T[]>;
  create(body: T, signal?: AbortSignal): Promise<T>;
  update(id: string, body: T, signal?: AbortSignal): Promise<T>;
  remove(id: string, signal?: AbortSignal): Promise<void>;
}

export function crudClient<T extends { id: string }>(kind: ConfigEntityKind): CrudClient<T> {
  const base = `${API_BASE}/api/config/${kind}`;
  return {
    kind,
    async list(signal) {
      return readJson<T[]>(await fetch(base, { signal }));
    },
    async create(body, signal) {
      return readJson<T>(
        await fetch(base, {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(body),
          signal,
        }),
      );
    },
    async update(id, body, signal) {
      return readJson<T>(
        await fetch(`${base}/${encodeURIComponent(id)}`, {
          method: "PUT",
          headers: { "content-type": "application/json" },
          body: JSON.stringify(body),
          signal,
        }),
      );
    },
    async remove(id, signal) {
      const res = await fetch(`${base}/${encodeURIComponent(id)}`, {
        method: "DELETE",
        signal,
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
    },
  };
}

export const agentsApi = crudClient<StudioAgent>("agents");
export const trackersApi = crudClient<StudioTracker>("trackers");
export const reposApi = crudClient<StudioRepo>("repos");
export const projectsApi = crudClient<StudioProject>("projects");
export const mcpServersApi = crudClient<StudioMcpServer>("mcp-servers");
export const secretsApi = crudClient<StudioSecret>("secrets");

export async function fetchChanges(signal?: AbortSignal): Promise<ConfigChange[]> {
  return readJson<ConfigChange[]>(await fetch(`${API_BASE}/api/config/changes`, { signal }));
}

export async function revertChange(id: string, signal?: AbortSignal): Promise<void> {
  const res = await fetch(
    `${API_BASE}/api/config/changes/${encodeURIComponent(id)}/revert`,
    { method: "POST", signal },
  );
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
}
