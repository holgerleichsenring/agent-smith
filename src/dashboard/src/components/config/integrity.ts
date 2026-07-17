import type { ConfigEntityKind, StudioProject } from "@/lib/configApi";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: referential-integrity helpers. `resolves` answers "does this ref point
// at a real catalog entry"; `projectIntegrity` rolls the project's refs into a
// single pass/fail plus a per-ref breakdown so the drawer can gate Save and the
// preview can show exactly which reference is broken.

export function resolves(
  catalog: ConfigCatalog,
  kind: ConfigEntityKind,
  id: string,
): boolean {
  if (!id) return false;
  return catalog[kind].some((e) => e.id === id);
}

export interface ProjectIntegrity {
  agentOk: boolean;
  trackerOk: boolean;
  repoResults: Array<{ id: string; ok: boolean }>;
  reposOk: boolean;
  /** True only when agent + tracker + at least one repo all resolve. */
  ok: boolean;
}

export function projectIntegrity(
  catalog: ConfigCatalog,
  project: StudioProject,
): ProjectIntegrity {
  const agentOk = resolves(catalog, "agents", project.agent);
  const trackerOk = resolves(catalog, "trackers", project.tracker);
  const repoResults = project.repos.map((id) => ({
    id,
    ok: resolves(catalog, "repos", id),
  }));
  const reposOk = repoResults.length > 0 && repoResults.every((r) => r.ok);
  return {
    agentOk,
    trackerOk,
    repoResults,
    reposOk,
    ok: agentOk && trackerOk && reposOk,
  };
}
