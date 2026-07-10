import type { NodeStatus } from "@/components/execution/TimingGutter";

// p0208: RunSnapshot.status → NodeStatus. success→ok, failed|error→fail,
// running→run, else wait. Same palette as the p0205 NodeStatus rail.
// p0259: cancelled→cancel — a cancelled run gets its own glyph, never the fail ✕.
export function toNodeStatus(status: string): NodeStatus {
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
    default:
      return "wait";
  }
}
