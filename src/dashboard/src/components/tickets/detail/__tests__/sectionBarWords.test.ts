import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { pr, ticket } from "../../__tests__/ticketFixtures";
import { ciWords, pullRequestFact, stateWord, trackerFact } from "../sectionBarWords";
import { attempt } from "./detailFixtures";

const en = translator("en");

// 2026-11-17-8d1ca: the section bar names the ticket's state as the list names its bucket, and its facts from
// recorded fields only.

describe("sectionBarWords", () => {
  it("stateWord_NamesTheBucketAsTheListDoes", () => {
    expect(stateWord(en, ticket({ bucket: "working" }))).toBe("Working");
    expect(stateWord(en, ticket({ bucket: "stuck" }))).toBe("Stuck");
    expect(stateWord(en, ticket({ bucket: "idle" }))).toBe("Ended");
    expect(stateWord(en, ticket({ bucket: "unheard-of" as never }))).toBeNull();
  });

  it("trackerFact_NamesTrackerAndStatus_OnlyWhenRecorded", () => {
    expect(trackerFact(en, ticket({ platform: "jira", trackerState: "In Review" }))).toBe("Jira status In Review");
    expect(trackerFact(en, ticket({ platform: "github", trackerState: null }))).toBe("GitHub");
    expect(trackerFact(en, ticket({ platform: null, trackerState: "New" }))).toBe("tracker status New");
    expect(trackerFact(en, ticket({ platform: null, trackerState: null }))).toBeNull();
  });

  it("pullRequestFact_TellsTheFate_OrNoPR", () => {
    expect(pullRequestFact(en, ticket({ pullRequests: [pr({ state: "merged" }), pr({ number: "2", state: "open" })] }))).toBe("1 PR merged, 1 open");
    expect(pullRequestFact(en, ticket({ pullRequests: [] }))).toBe("no PR");
    const opened = attempt({ pullRequests: [{ repo: "portal-api", url: "https://example.test/pr/1" }] as never });
    expect(pullRequestFact(en, ticket({ pullRequests: [] }), [opened])).toBe("1 PR");
  });

  // 2026-11-17-3435a
  it("pullRequestFact_AddsTheOpenPullRequestsCi", () => {
    const red = { state: "red", headSha: "abc", at: null, failures: [{ name: "build", url: null, excerpt: "x" }, { name: "lint", url: null, excerpt: "y" }] };
    expect(pullRequestFact(en, ticket({ pullRequests: [pr({ state: "open", ci: red })] }))).toBe("1 PR open · CI red: build, lint");
    expect(ciWords(en, [pr({ state: "open", ci: { state: "pending", headSha: "abc", at: null, failures: [] } })])).toBe("CI pending");
    expect(ciWords(en, [pr({ state: "open", ci: { state: "green", headSha: "abc", at: null, failures: [] } })])).toBe("CI green");
    expect(ciWords(en, [pr({ state: "open", ci: { state: "none", headSha: "abc", at: null, failures: [] } })])).toBeNull();
    expect(ciWords(en, [pr({ state: "merged", ci: red })])).toBeNull();
  });
});
