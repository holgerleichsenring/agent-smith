// 2026-08-27-729e: what this installation is running. The numbers all existed and were
// readable nowhere — a version was visible exactly when it was WRONG, through the mismatch
// banner, and never when somebody simply wanted to know it.
//
// The server states its OWN build, the sandbox-agent build each project spawns, and the
// database behind both. It does not state the dashboard's release and cannot: the findings
// request names this bundle's REVISION only, and the caller's version is constructed as
// null on purpose, because a revision is what tells two builds of one release apart. So the
// dashboard's half is read from the constant its own bundle was stamped with, and labelled
// as its own.

import { getJson } from "@/lib/apiResponse";

/** Where a project's sandbox-agent tag came from. */
export type AgentVersionSource = "pinned" | "derived" | "underivable";

export interface SandboxAgentRelease {
  project: string;
  /** null on a build that carries no release — there is nothing to derive from. */
  version: string | null;
  source: AgentVersionSource | string;
}

export interface DatabaseIdentity {
  provider: string;
  reachable: boolean;
  pendingMigrations: number;
  error: string | null;
}

// 2026-09-20-4981: which skill catalog this installation is bound to. No root: this
// report is read without signing in, and a mounted catalog's root is an operator's own
// directory. The catalog browser, which needs a catalog permission, carries the full phrase.
export interface CatalogBinding {
  /** "default", "path", "url" or "embedded". */
  source: string;
  /** The resolved tag, or the configured pin when resolution failed; null when unpinned. */
  version: string | null;
  /** Fingerprint of a materialized overlay, or null. */
  overlay: string | null;
  /** False when this is only what was CONFIGURED — the catalog did not resolve. */
  resolved: boolean;
}

export interface InstallationIdentity {
  serverRelease: string | null;
  serverRevision: string | null;
  agents: SandboxAgentRelease[];
  database: DatabaseIdentity;
  /** What the server resolved — the primary fact. */
  catalog: CatalogBinding;
  /** The floor the binary was built against. Differs from the binding by design on three
   * of the four source modes, which is why it is reported beside it and never instead. */
  embeddedCatalogVersion: string | null;
}

export async function fetchInstallationIdentity(
  signal?: AbortSignal,
): Promise<InstallationIdentity> {
  return getJson<InstallationIdentity>("/api/config/installation", signal);
}
