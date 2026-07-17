"use client";

import { useCallback, useEffect, useState } from "react";
import type {
  StudioAgent,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioSecret,
  StudioTracker,
} from "@/lib/configApi";
import {
  agentsApi,
  trackersApi,
  reposApi,
  projectsApi,
  mcpServersApi,
  secretsApi,
} from "@/lib/configApi";

// p0345: loads all six catalogs at once. The whole catalog is needed even for a
// single list view because the FK pickers (agent/tracker/repos/secret) resolve
// against the OTHER catalogs — a project card can only render "agent → gpt-5"
// if the agents catalog is present.

export interface ConfigCatalog {
  agents: StudioAgent[];
  trackers: StudioTracker[];
  repos: StudioRepo[];
  projects: StudioProject[];
  "mcp-servers": StudioMcpServer[];
  secrets: StudioSecret[];
}

const EMPTY: ConfigCatalog = {
  agents: [],
  trackers: [],
  repos: [],
  projects: [],
  "mcp-servers": [],
  secrets: [],
};

export interface UseConfigCatalog {
  catalog: ConfigCatalog;
  loading: boolean;
  error: string | null;
  reload: () => Promise<void>;
}

export function useConfigCatalog(): UseConfigCatalog {
  const [catalog, setCatalog] = useState<ConfigCatalog>(EMPTY);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const [agents, trackers, repos, projects, mcp, secrets] = await Promise.all([
        agentsApi.list(signal),
        trackersApi.list(signal),
        reposApi.list(signal),
        projectsApi.list(signal),
        mcpServersApi.list(signal),
        secretsApi.list(signal),
      ]);
      setCatalog({ agents, trackers, repos, projects, "mcp-servers": mcp, secrets });
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      setError((err as Error).message);
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
