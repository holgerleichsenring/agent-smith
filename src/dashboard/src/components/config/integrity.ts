import type { ConfigEntityKind, StudioProject } from "@/lib/configApi";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: referential-integrity helpers. `resolves` answers "does this ref point
// at a real catalog entry"; `projectIntegrity` rolls the project's refs into a
// single pass/fail plus a per-ref breakdown so the drawer can gate Save and the
// preview can show exactly which reference is broken.
// p0345b: a project repo ref has two forms — a plain id validating against the
// repos catalog, and a connection-scoped discovery ref "conn/RepoName" (p0281a)
// that is VALID iff connection "conn" exists. Neither form is flagged dangling
// when its catalog entry exists.

export function resolves(
  catalog: ConfigCatalog,
  kind: ConfigEntityKind,
  id: string,
): boolean {
  if (!id) return false;
  return catalog[kind].some((e) => e.id === id);
}

/** How a project repo ref resolved: against the repos catalog, against a
 *  connection (conn-scoped discovery ref), or not at all. */
export type RepoRefVia = "repo" | "connection" | null;

export interface RepoRefResult {
  id: string;
  ok: boolean;
  via: RepoRefVia;
}

/**
 * Resolve one project repo ref. "conn/RepoName" (first slash splits) resolves
 * iff the connection exists AND a repo name is present after the slash; a
 * plain ref resolves against the repos catalog.
 */
export function resolveRepoRef(catalog: ConfigCatalog, ref: string): RepoRefResult {
  if (!ref) return { id: ref, ok: false, via: null };
  const slash = ref.indexOf("/");
  if (slash > 0) {
    const connection = ref.slice(0, slash);
    const repoName = ref.slice(slash + 1);
    const ok = repoName.length > 0 && resolves(catalog, "connections", connection);
    return { id: ref, ok, via: ok ? "connection" : null };
  }
  const ok = resolves(catalog, "repos", ref);
  return { id: ref, ok, via: ok ? "repo" : null };
}

export interface ProjectIntegrity {
  agentOk: boolean;
  trackerOk: boolean;
  repoResults: RepoRefResult[];
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
  const repoResults = project.repos.map((id) => resolveRepoRef(catalog, id));
  const reposOk = repoResults.length > 0 && repoResults.every((r) => r.ok);
  return {
    agentOk,
    trackerOk,
    repoResults,
    reposOk,
    ok: agentOk && trackerOk && reposOk,
  };
}
