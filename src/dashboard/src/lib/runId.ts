// p0441: a run id shortened at the END, where it differs.
//
// A run id reads `2026-08-17T21-30-46-a98c`: a timestamp and then the short hash that
// actually tells two runs apart. Six places cut it with `slice(0, 8)`, which yields
// `2026-08-` — identical for every run of the same day, and the only part nobody needs.
// The header showed `#2026-08-` where the operator (and this codebase's own commit
// messages) say "run a98c".

/** The distinctive tail of a run id, or the whole thing when it has no tail to speak of. */
export function shortRunId(runId: string): string {
  if (!runId) return "";
  const tail = runId.slice(runId.lastIndexOf("-") + 1);
  // A trailing segment only identifies the run when it is a short token of its own; ids
  // that do not follow the convention keep their last characters rather than their first.
  if (tail.length >= 3 && tail.length <= 12 && tail !== runId) return tail;
  return runId.length > 12 ? runId.slice(-8) : runId;
}
