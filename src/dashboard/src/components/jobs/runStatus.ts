import type { NodeStatus } from "@/components/execution/TimingGutter";

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
    default:
      return "wait";
  }
}
