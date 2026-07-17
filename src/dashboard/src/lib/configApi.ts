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
  /** p0345c drift facts: where the runtime's agentsmith.yml lives, when the
   *  file last changed on disk, and when the runtime last actually READ it.
   *  fileModifiedAt > lastReadAt is the drift alarm. */
  configPath: string;
  fileModifiedAt: string | null;
  lastReadAt: string | null;
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
  | "connections"
  | "repos"
  | "projects"
  | "mcp-servers"
  | "secrets";

// --- p0345c: the CAPABILITIES descriptor — backend truth about which tracker/
// connection types exist (and their per-type field sets), which agent providers
// and resolution strategies are known, and the pipeline names. The forms render
// FROM this; no type knowledge is hardcoded client-side.

export interface CapabilityField {
  key: string;
  label: string;
  required: boolean;
}

export interface TrackerTypeDescriptor {
  type: string;
  fields: CapabilityField[];
}

export interface ConnectionTypeDescriptor {
  type: string;
  /** What this connection type calls its org scope (e.g. "organization" for
   *  Azure DevOps, "owner" for GitHub) — labels the org field in the form. */
  orgLabel: string;
  fields: CapabilityField[];
}

export interface ConfigCapabilities {
  trackerTypes: TrackerTypeDescriptor[];
  connectionTypes: ConnectionTypeDescriptor[];
  agentProviders: string[];
  resolutionStrategies: string[];
  pipelines: string[];
}

export async function fetchCapabilities(signal?: AbortSignal): Promise<ConfigCapabilities> {
  return readJson<ConfigCapabilities>(await fetch(`${API_BASE}/api/config/capabilities`, { signal }));
}

/** p0345c: one repo the discovery cache knows inside a connection. */
export interface DiscoveredRepo {
  name: string;
  defaultBranch: string | null;
}

/** The discovery snapshot for one connection — discoveredAt null means the
 *  discovery never ran (the honest "not discovered yet" state). */
export interface ConnectionRepos {
  discoveredAt: string | null;
  repos: DiscoveredRepo[];
}

export async function fetchConnectionRepos(
  connectionId: string,
  signal?: AbortSignal,
): Promise<ConnectionRepos> {
  return readJson<ConnectionRepos>(
    await fetch(`${API_BASE}/api/config/connections/${encodeURIComponent(connectionId)}/repos`, {
      signal,
    }),
  );
}

/** One per-role model entry (p0345c AgentEntity v2) — model name plus the
 *  optional Azure deployment and per-call token cap. */
export interface AgentModelEntry {
  model: string;
  deployment?: string;
  maxTokens?: number;
}

export interface AgentPricingEntry {
  inputPerMillion: number;
  outputPerMillion: number;
  cacheReadPerMillion?: number;
}

export interface AgentCacheConfig {
  isEnabled: boolean;
  strategy: string;
}

export interface AgentCompactionConfig {
  isEnabled: boolean;
  thresholdIterations: number;
  maxContextTokens: number;
  keepRecentIterations: number;
  summaryModel: string;
}

export interface AgentRetryConfig {
  maxRetries: number;
  initialDelayMs: number;
  backoffMultiplier: number;
  maxDelayMs: number;
}

/** p0345c AgentEntity v2 — the FULL raw surface the loader deserializes:
 *  provider/endpoint, per-role model entries, and the optional pricing/cache/
 *  compaction/retry sections (absent sections stay absent — only non-empty
 *  sections are persisted). `keySecret` is honestly nullable. */
export interface StudioAgent {
  id: string;
  provider: string;
  keySecret: string | null;
  endpoint?: string;
  apiVersion?: string;
  networkTimeoutSeconds?: number;
  models: Record<string, AgentModelEntry>;
  pricing?: { models: Record<string, AgentPricingEntry> };
  cache?: AgentCacheConfig;
  compaction?: AgentCompactionConfig;
  retry?: AgentRetryConfig;
}

export interface TrackerPollingConfig {
  enabled: boolean;
  intervalSeconds: number;
  jitterPercent: number;
}

/** p0345c TrackerEntity v2 — type + auth are the invariants; every other field
 *  is per-type and rendered from the capabilities descriptor. */
export interface StudioTracker {
  id: string;
  type: string;
  authSecret: string;
  url?: string;
  organization?: string;
  project?: string;
  openStates?: string[];
  doneStatus?: string;
  failedStatus?: string;
  triggerStatuses?: string[];
  pipelineFromLabel?: string;
  polling?: TrackerPollingConfig;
}

/** p0345b: a repo-discovery connection (p0281a) — org/project scope + a FK to
 *  the secret holding the auth token. Project repo refs of the form
 *  "{connection}/{RepoName}" resolve against these instead of the repos
 *  catalog. p0345c: type + auth are the invariants; the scope fields are
 *  per-type (rendered from the capabilities descriptor, orgLabel names the
 *  org field). */
export interface StudioConnection {
  id: string;
  type: string;
  authSecret: string;
  organization?: string;
  project?: string;
  defaultBranch?: string;
}

export interface StudioRepo {
  id: string;
  name: string;
  branch: string;
}

/** p0345c: how a ticket resolves to THIS project — a strategy from the
 *  backend's resolver registry (tag / area_path / repo / to_address / …) plus
 *  the strategy's match value. */
export interface ProjectResolution {
  strategy: string;
  value: string;
}

/** The relational heart: agent + tracker are single FKs, repos a FK set.
 *  p0345c truth-fix: the field once mislabeled `trigger` IS the pipeline —
 *  renamed on the wire; `resolution` is a strategy choice, not freetext. */
export interface StudioProject {
  id: string;
  agent: string;
  tracker: string;
  repos: string[];
  pipeline: string;
  pipelines: string[];
  resolution: ProjectResolution | null;
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
  | StudioConnection
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
export const connectionsApi = crudClient<StudioConnection>("connections");
export const reposApi = crudClient<StudioRepo>("repos");
export const projectsApi = crudClient<StudioProject>("projects");
export const mcpServersApi = crudClient<StudioMcpServer>("mcp-servers");
export const secretsApi = crudClient<StudioSecret>("secrets");

/** p0343b: the catalog rendered as loader-round-trippable agentsmith.yml —
 *  GET /api/config/export.yml (text/yaml). */
export async function fetchConfigExportYml(signal?: AbortSignal): Promise<string> {
  const res = await fetch(`${API_BASE}/api/config/export.yml`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return await res.text();
}

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
