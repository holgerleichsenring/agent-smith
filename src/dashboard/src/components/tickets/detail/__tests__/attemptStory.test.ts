import { describe, expect, it } from "vitest";
import { attemptStory, liveSpec } from "../attemptStory";
import { attempt, entry, pass, passes, series, spec } from "./detailFixtures";

// 2026-10-13-9a59s: an attempt card's sentences come from fields; an absent field drops its clause.

const executed = (specId: string, ordinal: number, status: string) => ({ specId, ordinal, title: specId, status, verdict: null });

describe("attemptStory", () => {
  it("AHandedBackAttempt_EndsWithItsCase_AndListsNoSpec", () => {
    const handedBack = attempt({ runId: "r-1", status: "failed", handedBack: true, executedSpecs: [executed("s1", 1, "not_started")] });
    const cut = series("r-1", [], { handback: { case: "RequirementsContradictRepository", runId: "r-1", attempt: 1, at: "2026-09-04T14:10:00Z" } });
    const story = attemptStory({ attempt: handedBack, passes: null, cutEntries: [], cut });
    expect(story.ended).toEqual({ lead: "Handed back as contradiction.", rest: null });
    expect(story.did).toBeNull();
    expect(story.doing).toBeNull();
  });

  it("AFailedAttempt_EndsWithTheFailingSpecsLastPassEndReason_AndAnAbsentPassCountDropsTheClause", () => {
    const failed = attempt({
      status: "failed", executedSpecs: [executed("s1", 1, "done"), executed("s3", 3, "failed")],
    });
    const read = passes("r-1", {
      s1: [pass()],
      s3: [pass({ ordinal: 2, endedBecause: "re-engaged" }), pass({ ordinal: 3, passInSpec: 2, endedBecause: "budget" })],
    });
    expect(attemptStory({ attempt: failed, passes: read, cutEntries: [], cut: null }).ended)
      .toEqual({ lead: "Failed:", rest: "spec 3 not done after 2 passes — the last pass budget spent." });
    expect(attemptStory({ attempt: failed, passes: null, cutEntries: [], cut: null }).ended)
      .toEqual({ lead: "Failed:", rest: "spec 3 not done." });
  });

  it("AFailingSpecWhoseLastPassEndedUnknown_SaysFailedWithNoReasonClause", () => {
    const failed = attempt({ status: "failed", executedSpecs: [executed("s3", 3, "failed")] });
    const read = passes("r-1", { s3: [pass({ endedBecause: "unknown" })] });
    expect(attemptStory({ attempt: failed, passes: read, cutEntries: [], cut: null }).ended?.rest)
      .toBe("spec 3 not done after 1 pass.");
  });

  it("Did_NeverSaysAPrWasMarkedReady", () => {
    const done = attempt({
      status: "success", executedSpecs: [executed("s1", 1, "done"), executed("s2", 2, "done")],
      pullRequests: [
        { repo: "portal-api", status: "opened", url: "https://github.com/o/portal-api/pull/812", reason: null, openedAt: "2026-09-04T14:20:00Z" },
        { repo: "portal-worker", status: "no_changes", url: null, reason: null, openedAt: "2026-09-04T14:20:00Z" },
      ],
    });
    const read = passes("r-1", {
      s1: [pass()],
      s2: [pass({ endedBecause: "question" }), pass({ ordinal: 2, passInSpec: 2, openedBecause: "after-answer" })],
    });
    const cutEntries = [
      entry({ source: "spec-cut", kind: "cut-review", detail: { attempt: 1, deliverable: "refused", findings: [{}] } }),
      entry({ source: "spec-cut", kind: "cut-review", detail: { attempt: 2, deliverable: "accepted", findings: [] } }),
    ];
    const story = attemptStory({ attempt: done, passes: read, cutEntries, cut: null });
    expect(story.did).toEqual([
      "Cut 1: refused by the reviewer (1 finding)",
      "Cut 2: accepted by the reviewer",
      "Opened the PR #812 (portal-api)",
      "Spec 1: done in one pass",
      "Spec 2: done in 2 passes, asked 1 question, resumed once after an answer",
    ]);
    expect(story.did!.join(" ")).not.toMatch(/ready/i);
    expect(story.ended).toEqual({ lead: "Finished.", rest: null });
  });

  it("ALiveDerivingAttempt_OnARunRecordSeries_ReadsDeriving_NotASpecOfTheOldSet", () => {
    const live = attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null });
    const old = [spec({ specId: "p1a", fileStem: null, state: "in_progress" })];
    const byDefault = series("", old, { runId: null, attempt: null, gate: null, source: "run-record" });
    const scoped = series("r-2", old, { gate: null, source: "run-record" });
    expect(liveSpec(live, byDefault)).toBeNull();
    expect(liveSpec(live, scoped)).toBeNull();
    expect(attemptStory({ attempt: live, passes: null, cutEntries: [], cut: byDefault }).doing).toEqual(["Deriving the series"]);
  });

  it("TheLiveAttempt_SaysSpecNOfMFromItsOwnCut_AndDerivingWhileTheCutIsAnEarlierAttempts", () => {
    const live = attempt({ number: 2, runId: "r-2", status: "running", finishedAt: null });
    const own = series("r-2", [
      spec({ specId: "s1", ordinal: 1, state: "carried" }),
      spec({ specId: "s2", ordinal: 2, state: "in_progress", worked: [{ stepId: "1", state: "done", doneByRunId: "r-2", doneByAttempt: 2, doneInPass: 1 }] }),
      spec({ specId: "s3", ordinal: 3, state: "not_started" }),
    ]);
    expect(liveSpec(live, own)).toMatchObject({ of: 3, step: 2, steps: 2 });
    expect(attemptStory({ attempt: live, passes: null, cutEntries: [], cut: own }).doing)
      .toEqual(["Spec 2, step 2: Port publishers", "Next: spec 3"]);
    const earlier = series("r-1", own.specs);
    expect(liveSpec(live, earlier)).toBeNull();
    expect(attemptStory({ attempt: live, passes: null, cutEntries: [], cut: earlier }).doing).toEqual(["Deriving the series"]);
  });
});
