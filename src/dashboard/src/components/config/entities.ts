import type {
  ConfigChangeKind,
  ConfigEntityKind,
  CrudClient,
  StudioEntity,
} from "@/lib/configApi";
import {
  agentsApi,
  trackersApi,
  connectionsApi,
  reposApi,
  projectsApi,
  mcpServersApi,
  designSourcesApi,
  secretsApi,
} from "@/lib/configApi";

// p0345/p0345b: static metadata for the editable entity kinds (2026-10-01-7f7aa: eight). Labels
// drive the tabs/headers; `client` binds each kind to its typed CRUD endpoint;
// `blank` mints an empty draft for the "New" flow. Refs inside a draft start
// empty and are filled by picking from the catalog, never by typing.

export const ENTITY_KINDS: ConfigEntityKind[] = [
  "agents",
  "trackers",
  "connections",
  "repos",
  "projects",
  "mcp-servers",
  "design-sources",
  "secrets",
];

export const ENTITY_LABEL: Record<ConfigEntityKind, string> = {
  agents: "Agents",
  trackers: "Trackers",
  connections: "Connections",
  repos: "Repositories",
  projects: "Projects",
  "mcp-servers": "MCP servers",
  "design-sources": "Design sources",
  secrets: "Secrets",
};

export const ENTITY_SINGULAR: Record<ConfigEntityKind, string> = {
  agents: "Agent",
  trackers: "Tracker",
  connections: "Connection",
  repos: "Repository",
  projects: "Project",
  "mcp-servers": "MCP server",
  "design-sources": "Design source",
  secrets: "Secret",
};

// p0353: singular label for any change-feed row, including the settings singletons
// (which have no catalog nav of their own). The Changes view keys off this.
export const CHANGE_KIND_SINGULAR: Record<ConfigChangeKind, string> = {
  ...ENTITY_SINGULAR,
  settings: "Settings",
};

// p0343b: the one-line subtitle under each entity title in the studio content
// area (the mock's title row: entity title + subtitle + green New button).
export const ENTITY_SUBTITLE: Record<ConfigEntityKind, string> = {
  agents: "LLM providers and their per-role models",
  trackers: "ticket sources — polling and webhook triggers",
  connections: "repo-discovery scopes — org/project + auth",
  repos: "individual repositories the pipelines work on",
  projects: "the wiring — agent → project ← tracker · repos",
  "mcp-servers": "external MCP tool servers",
  "design-sources": "design tools projects read designs from — token by secret name",
  secrets: "env-names only — values stay in the runtime store",
};

// p0343c: the mock's per-entity glyphs (rail .ni + card .ec-ic + drawer .dh-ic).
export const ENTITY_ICON: Record<ConfigEntityKind, string> = {
  agents: "✦",
  trackers: "◱",
  connections: "◳",
  repos: "⎇",
  projects: "◈",
  "mcp-servers": "⇄",
  "design-sources": "◩",
  secrets: "◍",
};

// The type badge shown on each entity card.
export const ENTITY_BADGE: Record<ConfigEntityKind, string> = {
  agents: "agent",
  trackers: "tracker",
  connections: "connection",
  repos: "repo",
  projects: "project",
  "mcp-servers": "mcp",
  "design-sources": "design",
  secrets: "secret",
};

// A single typed client per kind, indexable by kind. The `unknown`-narrowing at
// the boundary keeps the map homogeneous while callers keep their concrete type.
export const ENTITY_CLIENT: Record<ConfigEntityKind, CrudClient<StudioEntity & { id: string }>> = {
  agents: agentsApi as unknown as CrudClient<StudioEntity & { id: string }>,
  trackers: trackersApi as unknown as CrudClient<StudioEntity & { id: string }>,
  connections: connectionsApi as unknown as CrudClient<StudioEntity & { id: string }>,
  repos: reposApi as unknown as CrudClient<StudioEntity & { id: string }>,
  projects: projectsApi as unknown as CrudClient<StudioEntity & { id: string }>,
  "mcp-servers": mcpServersApi as unknown as CrudClient<StudioEntity & { id: string }>,
  "design-sources": designSourcesApi as unknown as CrudClient<StudioEntity & { id: string }>,
  secrets: secretsApi as unknown as CrudClient<StudioEntity & { id: string }>,
};

export function blankEntity(kind: ConfigEntityKind): StudioEntity {
  switch (kind) {
    case "agents":
      // p0345c: optional sections (pricing/cache/compaction/retry) stay absent until
      // the operator adds them, so they are never persisted untouched.
      // 2026-09-30-62bab: models are declared in the catalog; roles name entries.
      return { id: "", provider: "", catalog: {}, models: {}, keySecret: null };
    case "trackers":
      // p0345c: everything beyond type + auth is per-type and comes from the
      // capabilities descriptor once a type is picked.
      return { id: "", type: "", authSecret: "" };
    case "connections":
      return { id: "", type: "", organization: "", project: "", authSecret: "", defaultBranch: "main" };
    case "repos":
      return { id: "", name: "", branch: "main" };
    case "projects":
      return { id: "", agent: "", tracker: "", repos: [], pipeline: "", pipelines: [], resolution: null };
    case "mcp-servers":
      return { id: "", transport: "http", url: "", authSecret: "" };
    case "design-sources":
      return { id: "", vendor: "figma", authSecret: "" };
    case "secrets":
      return { id: "" };
  }
}

export function isConfigEntityKind(value: string | undefined): value is ConfigEntityKind {
  return !!value && (ENTITY_KINDS as string[]).includes(value);
}
