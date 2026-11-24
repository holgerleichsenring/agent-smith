// 2026-11-23-933dc: the dashboard's address of one PoC revision — the page that shows its scan verdict.

export function pocRevisionPagePath(pocId: number, revision: number): string {
  return `/pocs/${encodeURIComponent(String(pocId))}/revisions/${encodeURIComponent(String(revision))}`;
}

/** 2026-11-23-c81ec: the dashboard's address of one PoC rollout — the steps of the run that deployed it. */
export function pocRolloutPagePath(pocId: number, runId: string): string {
  return `/pocs/${encodeURIComponent(String(pocId))}/rollouts/${encodeURIComponent(runId)}`;
}
