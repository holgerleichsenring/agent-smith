"use client";

import { useCallback, useEffect, useState } from "react";
import type {
  StudioAgent,
  StudioConnection,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioSecret,
  StudioTracker,
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

// p0345: loads all seven catalogs at once. The whole catalog is needed even for
// a single list view because the FK pickers (agent/tracker/repos/secret) resolve
// against the OTHER catalogs — a project card can only render "agent → gpt-5"
// if the agents catalog is present. p0345b adds connections: conn-scoped repo
// refs ("conn/Name") resolve against it.

export interface ConfigCatalog {
  agents: StudioAgent[];
  trackers: StudioTracker[];
  connections: StudioConnection[];
  repos: StudioRepo[];
  projects: StudioProject[];
  "mcp-servers": StudioMcpServer[];
  secrets: StudioSecret[];
}

const EMPTY: ConfigCatalog = {
  agents: [],
  trackers: [],
  connections: [],
  repos: [],
  projects: [],
  "mcp-servers": [],
  secrets: [],
};

export interface UseConfigCatalog {
  catalog: ConfigCatalog;
  loading: boolean;
  /** The thrown value, not its message — the studio renders a refusal as a state. */
  error: Error | null;
  reload: () => Promise<void>;
}

/**
 * By id, case-insensitively. Only the top-level catalogs: a project's own `repos` and
 * `templates` stay in declaration order, which 2026-09-15-a2d0 pinned as load-bearing.
 */
function byId<T extends { id: string }>(rows: T[]): T[] {
  return [...rows].sort((a, b) =>
    a.id.localeCompare(b.id, undefined, { sensitivity: "base" }));
}

export function useConfigCatalog(): UseConfigCatalog {
  const [catalog, setCatalog] = useState<ConfigCatalog>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const [agents, trackers, connections, repos, projects, mcp, secrets] = await Promise.all([
        agentsApi.list(signal),
        trackersApi.list(signal),
        connectionsApi.list(signal),
        reposApi.list(signal),
        projectsApi.list(signal),
        mcpServersApi.list(signal),
        secretsApi.list(signal),
      ]);
      // 2026-09-15-9b3e: sorted HERE, not at the render sites. The store returns a SELECT
      // with no ORDER BY, and ten places render one of these arrays — the cards, the agent,
      // tracker, repos and three secret pickers, the key-secret picker, the connection
      // dropdown, the repo inventory and the template editor's project list. Sorting once
      // where they are loaded reaches every one; sorting where they are drawn reaches one.
      setCatalog({
        agents: byId(agents),
        trackers: byId(trackers),
        connections: byId(connections),
        repos: byId(repos),
        projects: byId(projects),
        "mcp-servers": byId(mcp),
        secrets: byId(secrets),
      });
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      setError(err as Error);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const reload = useCallback(() => load(), [load]);

  return { catalog, loading, error, reload };
}
