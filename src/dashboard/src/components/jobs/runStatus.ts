import type { NodeStatus } from "@/components/execution/TimingGutter";

// p0208: RunSnapshot.status → NodeStatus. success→ok, failed|error→fail,
// running→run, else wait. Same palette as the p0205 NodeStatus rail.
export function toNodeStatus(status: string): NodeStatus {
  switch (status.toLowerCase()) {
    case "success":
      return "ok";
    case "failed":
    case "error":
      return "fail";
    case "running":
      return "run";
    default:
      return "wait";
  }
}
