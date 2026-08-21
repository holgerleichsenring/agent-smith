// p0490: what a pull-request status MEANS, in one place. Three surfaces answer the
// same two questions — is there a pull request to link to, and is it still open — and
// before init learned to finish its own pull requests, "opened" answered both. It no
// longer does: a completed one exists but is not open, and a refused completion is
// still open. One module so the rail count, the list page and the run detail cannot
// drift apart on that.

import type { PullRequestStatus } from "@/types/hub-events";

/** A pull request exists on the platform and has a URL worth linking to. */
export function hasPullRequest(status: PullRequestStatus): boolean {
  return (
    status === "opened" ||
    status === "completed" ||
    status === "completion_armed" ||
    status === "completion_refused"
  );
}

/** The pull request is waiting for a PERSON — the count the operator acts on. p0501: an
 *  armed one is waiting too, but for a build, so counting it here would send the operator
 *  to look at something that is already finishing itself. */
export function isOpenPullRequest(status: PullRequestStatus): boolean {
  return status === "opened" || status === "completion_refused";
}

/** The short word shown on the row's status pill. */
export function pullRequestStatusLabel(status: PullRequestStatus): string {
  switch (status) {
    case "completed":
      return "merged";
    case "completion_armed":
      return "auto-merging";
    case "completion_refused":
      return "still open";
    case "no_changes":
      return "no changes";
    case "failed":
      return "failed";
    default:
      return "opened";
  }
}
