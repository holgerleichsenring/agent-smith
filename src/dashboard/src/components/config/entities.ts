import type {
  ConfigChangeKind,
  ConfigEntityKind,
  CrudClient,
  StudioEntity,
} from "@/lib/configApi";
import type { TextKey } from "@/lib/i18n/translate";
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

// 2026-11-23-5669d: the kinds' names as dictionary keys, for what the studio renders. ENTITY_LABEL stays in English for
// the header's place table (lib/places.ts), which reads strings. CHANGE_KIND_SINGULAR_KEY adds the settings singletons
// for the change feed (p0353).
export const ENTITY_LABEL_KEY = {
  agents: "config.kind.agents",
  trackers: "config.kind.trackers",
  connections: "config.kind.connections",
  repos: "config.kind.repos",
  projects: "config.kind.projects",
  "mcp-servers": "config.kind.mcpServers",
  "design-sources": "config.kind.designSources",
  secrets: "config.kind.secrets",
} as const satisfies Record<ConfigEntityKind, TextKey>;

export const ENTITY_SINGULAR_KEY = {
  agents: "config.kind.agent",
  trackers: "config.kind.tracker",
  connections: "config.kind.connection",
  repos: "config.kind.repo",
  projects: "config.kind.project",
  "mcp-servers": "config.kind.mcpServer",
  "design-sources": "config.kind.designSource",
  secrets: "config.kind.secret",
} as const satisfies Record<ConfigEntityKind, TextKey>;

/** The catalog's empty state, one sentence per kind. */
export const ENTITY_EMPTY_KEY = {
  agents: "config.kind.agents.empty",
  trackers: "config.kind.trackers.empty",
  connections: "config.kind.connections.empty",
  repos: "config.kind.repos.empty",
  projects: "config.kind.projects.empty",
  "mcp-servers": "config.kind.mcpServers.empty",
  "design-sources": "config.kind.designSources.empty",
  secrets: "config.kind.secrets.empty",
} as const satisfies Record<ConfigEntityKind, TextKey>;

export const CHANGE_KIND_SINGULAR_KEY = {
  ...ENTITY_SINGULAR_KEY,
  settings: "config.kind.settings",
} as const satisfies Record<ConfigChangeKind, TextKey>;


// p0343b: the one-line subtitle under each entity title in the studio content
// area (the mock's title row: entity title + subtitle + green New button).
export const ENTITY_SUBTITLE = {
  agents: "config.kind.agents.sub",
  trackers: "config.kind.trackers.sub",
  connections: "config.kind.connections.sub",
  repos: "config.kind.repos.sub",
  projects: "config.kind.projects.sub",
  "mcp-servers": "config.kind.mcpServers.sub",
  "design-sources": "config.kind.designSources.sub",
  secrets: "config.kind.secrets.sub",
} as const satisfies Record<ConfigEntityKind, TextKey>;

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
export const ENTITY_BADGE = {
  agents: "config.badge.agent",
  trackers: "config.badge.tracker",
  connections: "config.badge.connection",
  repos: "config.badge.repo",
  projects: "config.badge.project",
  "mcp-servers": "config.badge.mcp",
  "design-sources": "config.badge.design",
  secrets: "config.badge.secret",
} as const satisfies Record<ConfigEntityKind, TextKey>;

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
