// 2026-10-16-86e6c: the run viewer's address for one run, and its trace's — one spelling for every link to a run.

export function runPath(runId: string): string {
  return `/jobs/${encodeURIComponent(runId)}`;
}

export function runTracePath(runId: string): string {
  return `${runPath(runId)}/why`;
}
