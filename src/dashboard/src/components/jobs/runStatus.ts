import type { NodeStatus } from "@/components/execution/TimingGutter";

// 2026-09-17-042ef: the run status IN WORDS, moved here from RunCard, whose own comment said a
// second copy would be two answers waiting to disagree — and the Work it out page is where the
// second copy was about to be written. A word this map does not carry is a column value nothing
// has named yet: it is shown with its underscores opened rather than hidden or guessed at.
const STATUS_WORD: Record<string, string> = {
  running: "running",
  success: "success",
  shortfall: "done, with a shortfall",
  failed: "failed",
  error: "error",
  cancelled: "cancelled",
  // p0269a: capacity-waiting run — the ticket re-runs automatically when room frees.
  queued: "queued — waiting for capacity",
  // p0327: parked on a question — resumes as the same run once answered.
  waiting_for_input: "waiting for your input",
};

/** What a run's status column says, in the words an operator reads. */
export function runStatusWord(status: string | null | undefined): string {
  const said = (status ?? "").toLowerCase();
  return STATUS_WORD[said] ?? said.replace(/_/g, " ");
}

// p0208: RunSnapshot.status → NodeStatus. success→ok, failed|error→fail,
// running→run, else wait. Same palette as the p0205 NodeStatus rail.
// p0259: cancelled→cancel — a cancelled run gets its own glyph, never the fail ✕.
// 2026-08-25-39ab: a snapshot the server answered without a status is a run
// whose state is not yet known — "wait", the same answer an unrecognised word
// gets. Guessing a terminal state from an absent field would be worse than the
// throw this replaces.
export function toNodeStatus(status: string | null | undefined): NodeStatus {
  if (!status) return "wait";
  switch (status.toLowerCase()) {
    case "success":
      return "ok";
    case "failed":
    case "error":
      return "fail";
    case "cancelled":
      return "cancel";
    case "running":
      return "run";
    // p0269a/p0320d: a capacity-deferred run waits for room — its own amber
    // identity, distinct from the neutral "wait" (it is queued, not stalled).
    case "queued":
      return "queued";
    // p0327: parked on a DialogQuestion — waiting for the OPERATOR, not for
    // capacity; resumes as the same run once the answer arrives.
    case "waiting_for_input":
      return "input";
    // p0439: delivered with a shortfall — its own identity, counted among the dones.
    case "shortfall":
      return "shortfall";
    default:
      return "wait";
  }
}
