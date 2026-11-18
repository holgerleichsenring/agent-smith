import { describe, expect, it } from "vitest";
import { pr, ticket } from "../../__tests__/ticketFixtures";
import { ciWords, pullRequestFact, stateWord, trackerFact } from "../sectionBarWords";
import { attempt } from "./detailFixtures";

// 2026-11-17-8d1ca: the section bar names the ticket's state as the list names its bucket, and its facts from
// recorded fields only.

describe("sectionBarWords", () => {
  it("stateWord_NamesTheBucketAsTheListDoes", () => {
    expect(stateWord(ticket({ bucket: "waiting_on_review" }))).toBe("In review");
    expect(stateWord(ticket({ bucket: "stuck" }))).toBe("Stuck");
    expect(stateWord(ticket({ bucket: "idle" }))).toBe("Ended");
    expect(stateWord(ticket({ bucket: "unheard-of" as never }))).toBeNull();
  });

  it("trackerFact_NamesTrackerAndStatus_OnlyWhenRecorded", () => {
    expect(trackerFact(ticket({ platform: "jira", trackerState: "In Review" }))).toBe("Jira status In Review");
    expect(trackerFact(ticket({ platform: "github", trackerState: null }))).toBe("GitHub");
    expect(trackerFact(ticket({ platform: null, trackerState: "New" }))).toBe("tracker status New");
    expect(trackerFact(ticket({ platform: null, trackerState: null }))).toBeNull();
  });

  it("pullRequestFact_TellsTheFate_OrNoPR", () => {
    expect(pullRequestFact(ticket({ pullRequests: [pr({ state: "merged" }), pr({ number: "2", state: "open" })] }))).toBe("1 PR merged, 1 open");
    expect(pullRequestFact(ticket({ pullRequests: [] }))).toBe("no PR");
    const opened = attempt({ pullRequests: [{ repo: "portal-api", url: "https://example.test/pr/1" }] as never });
    expect(pullRequestFact(ticket({ pullRequests: [] }), [opened])).toBe("1 PR");
  });

  // 2026-11-17-3435a
  it("pullRequestFact_AddsTheOpenPullRequestsCi", () => {
    const red = { state: "red", headSha: "abc", at: null, failures: [{ name: "build", url: null, excerpt: "x" }, { name: "lint", url: null, excerpt: "y" }] };
    expect(pullRequestFact(ticket({ pullRequests: [pr({ state: "open", ci: red })] }))).toBe("1 PR open · CI red: build, lint");
    expect(ciWords([pr({ state: "open", ci: { state: "pending", headSha: "abc", at: null, failures: [] } })])).toBe("CI pending");
    expect(ciWords([pr({ state: "open", ci: { state: "green", headSha: "abc", at: null, failures: [] } })])).toBe("CI green");
    expect(ciWords([pr({ state: "open", ci: { state: "none", headSha: "abc", at: null, failures: [] } })])).toBeNull();
    expect(ciWords([pr({ state: "merged", ci: red })])).toBeNull();
  });
});
