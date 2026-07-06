import { EventType, type RunEvent } from "@/types/hub-events";

/**
 * The repos a run touches, for the run-detail header. Sourced from the snapshot's
 * configured repo list, falling back to the RunStarted event's repo list when the
 * snapshot hasn't captured it yet.
 *
 * Deliberately does NOT read SandboxCreatedEvent: its `repo` field carries the composite
 * sandbox KEY (`<repo>-<lang>-<size>`, e.g. "…Server-c#-2-2gi"), so a repo with several
 * toolchain/resource sandboxes leaked phantom per-sandbox entries into the header (a
 * 3-repo run showed 5 badges).
 */
export function deriveRunRepoNames(
  snapshotRepos: readonly string[] | undefined,
  events: readonly RunEvent[],
): string[] {
  const repos = new Set<string>();
  if (snapshotRepos) for (const r of snapshotRepos) repos.add(r);
  for (const e of events)
    if (e.type === EventType.RunStarted) for (const r of e.repos) repos.add(r);
  return [...repos].sort();
}
