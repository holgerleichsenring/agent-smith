import type {
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
  secretsApi,
} from "@/lib/configApi";

// p0345/p0345b: static metadata for the seven editable entity kinds. Labels
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
  "secrets",
];

export const ENTITY_LABEL: Record<ConfigEntityKind, string> = {
  agents: "Agents",
  trackers: "Trackers",
  connections: "Connections",
  repos: "Repositories",
  projects: "Projects",
  "mcp-servers": "MCP servers",
  secrets: "Secrets",
};

export const ENTITY_SINGULAR: Record<ConfigEntityKind, string> = {
  agents: "Agent",
  trackers: "Tracker",
  connections: "Connection",
  repos: "Repository",
  projects: "Project",
  "mcp-servers": "MCP server",
  secrets: "Secret",
};

// The type badge shown on each entity card.
export const ENTITY_BADGE: Record<ConfigEntityKind, string> = {
  agents: "agent",
  trackers: "tracker",
  connections: "connection",
  repos: "repo",
  projects: "project",
  "mcp-servers": "mcp",
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
  secrets: secretsApi as unknown as CrudClient<StudioEntity & { id: string }>,
};

export function blankEntity(kind: ConfigEntityKind): StudioEntity {
  switch (kind) {
    case "agents":
      return { id: "", provider: "", models: { coding: "", scan: "" }, keySecret: "" };
    case "trackers":
      return { id: "", type: "", org: "", project: "", authSecret: "" };
    case "connections":
      return { id: "", type: "", organization: "", project: "", authSecret: "", defaultBranch: "main" };
    case "repos":
      return { id: "", name: "", branch: "main" };
    case "projects":
      return { id: "", agent: "", tracker: "", repos: [], trigger: "", pipelines: [] };
    case "mcp-servers":
      return { id: "", transport: "http", url: "", authSecret: "" };
    case "secrets":
      return { id: "" };
  }
}

export function isConfigEntityKind(value: string | undefined): value is ConfigEntityKind {
  return !!value && (ENTITY_KINDS as string[]).includes(value);
}
