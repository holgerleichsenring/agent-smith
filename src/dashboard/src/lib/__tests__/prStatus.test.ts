import { describe, it, expect } from "vitest";
import { hasPullRequest, isOpenPullRequest, pullRequestStatusLabel } from "../prStatus";

// p0490: before init learned to finish its own pull requests, "opened" answered both
// "is there a PR" and "is it open". These pin the two questions apart, because three
// surfaces (rail count, list page, run detail) read the answer.

describe("prStatus", () => {
  it("hasPullRequest_EveryStatusThatProducedOne_IsTrue", () => {
    expect(hasPullRequest("opened")).toBe(true);
    expect(hasPullRequest("completed")).toBe(true);
    expect(hasPullRequest("completion_refused")).toBe(true);
    expect(hasPullRequest("completion_armed")).toBe(true);
    expect(hasPullRequest("no_changes")).toBe(false);
    expect(hasPullRequest("failed")).toBe(false);
  });

  it("isOpenPullRequest_AMergedOne_IsNoLongerOpen", () => {
    expect(isOpenPullRequest("opened")).toBe(true);
    // A refused completion left it open — it still waits for someone.
    expect(isOpenPullRequest("completion_refused")).toBe(true);
    expect(isOpenPullRequest("completed")).toBe(false);
    expect(isOpenPullRequest("failed")).toBe(false);
  });

  it("PrStatus_Armed_IsNotCountedAsWaitingForAHuman", () => {
    // p0501: an armed PR IS waiting — for a build, which nobody has to watch. Counting
    // it as open sends the operator to look at something already finishing itself.
    expect(isOpenPullRequest("completion_armed")).toBe(false);
    expect(hasPullRequest("completion_armed")).toBe(true);
  });

  it("pullRequestStatusLabel_SaysWhatHappened", () => {
    expect(pullRequestStatusLabel("opened")).toBe("opened");
    expect(pullRequestStatusLabel("completed")).toBe("merged");
    expect(pullRequestStatusLabel("completion_refused")).toBe("still open");
    expect(pullRequestStatusLabel("completion_armed")).toBe("auto-merging");
    expect(pullRequestStatusLabel("no_changes")).toBe("no changes");
    expect(pullRequestStatusLabel("failed")).toBe("failed");
  });
});
