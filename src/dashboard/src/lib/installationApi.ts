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

export interface InstallationIdentity {
  serverRelease: string | null;
  serverRevision: string | null;
  agents: SandboxAgentRelease[];
  database: DatabaseIdentity;
}

export async function fetchInstallationIdentity(
  signal?: AbortSignal,
): Promise<InstallationIdentity> {
  return getJson<InstallationIdentity>("/api/config/installation", signal);
}
