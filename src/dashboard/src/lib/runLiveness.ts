// p0388d: is this run still capable of producing events? Views that poll ask
// this and stop when the answer turns false, so a finished run costs nothing.
//
// "queued" and "waiting_for_input" are LIVE: both resume as the same run, so a
// view that stopped following them would freeze exactly where the operator is
// waiting for movement. Only the four terminal statuses end the story — the
// same set the run list uses to retire the cancel button (p0330).

export const TERMINAL_RUN_STATUSES: ReadonlySet<string> = new Set([
  "success",
  "failed",
  "error",
  "cancelled",
]);

export function isRunLive(status: string | null | undefined): boolean {
  if (!status) return false;
  return !TERMINAL_RUN_STATUSES.has(status.toLowerCase());
}
