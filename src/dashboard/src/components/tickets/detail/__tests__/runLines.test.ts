import { translator } from "@/lib/i18n/translate";
import { describe, expect, it } from "vitest";
import type { TicketDerivation } from "@/types/tickets";
import { attemptStatusInput, runStatus as runStatusIn } from "@/components/jobs/runlog/runStatusLine";
import { attemptListLine, deployListLine, derivationListLine, mergeListLine, runListLines } from "../runLines";
import { attempt } from "./detailFixtures";

const en = translator("en");
type DropLead<P extends unknown[], N extends number> = N extends 1 ? (P extends [unknown, ...infer R] ? R : never)
  : P extends [unknown, unknown, ...infer R] ? R : never;
const runStatus = (...args: DropLead<Parameters<typeof runStatusIn>, 1>) => runStatusIn(en, ...args);

// 2026-11-17-8d1cc: the Runs section's lines from recorded fields — every attempt says how it ended with its end kind.

const derivation = (over: Partial<TicketDerivation> = {}): TicketDerivation => ({
  runId: "d-1", status: "success", startedAt: "2026-09-04T09:00:00Z", finishedAt: "2026-09-04T09:04:10Z", cost: 0.42,
  outcome: "derived", specCount: 1, pullRequests: [], summary: null, ...over,
});

describe("runLines", () => {
  // 2026-11-22-7103a: the outcome and text are the run's status line — its head and its clauses.
  it("attemptListLine_NamesTheStatusHeadDurationCostAndTheRun", () => {
    const line = attemptListLine(attempt({ status: "failed", summary: "Pipeline aborted\nmore", cost: 0.71 }));
    expect(line).toMatchObject({ who: "Run 1", outcome: "Failed: Pipeline aborted", text: "", tone: "bad", href: "/jobs/r-1" });
    expect(line.meta).toMatch(/ · 30m 0s · \$0\.71$/);
  });

  it("AttemptListLine_Shortfall_SameHeadAndToneAsTheStatusLine", () => {
    const a = attempt({ status: "shortfall", executedSpecs: [
      { specId: "s1", ordinal: 1, title: "one", status: "done", verdict: null },
      { specId: "s2", ordinal: 2, title: "two", status: "failed", verdict: "No report was written. Retry." }] });
    const line = attemptListLine(a);
    const status = runStatus(attemptStatusInput(a));
    expect(line).toMatchObject({ outcome: status.head, tone: status.tone });
    expect(line).toMatchObject({ outcome: "1 of 2 specs delivered", text: "s2: No report was written.", tone: "bad" });
  });

  it("derivationListLine_SaysWhatCameOfIt_SoFarWhileRunning", () => {
    expect(derivationListLine(derivation())).toMatchObject({ who: "Derivation", outcome: "derived", text: "Spec derivation found 1 spec.", tone: "ok", attempt: null });
    expect(derivationListLine(derivation({ outcome: "running", finishedAt: null })).meta).toMatch(/\$0\.42 so far$/);
  });

  it("runListLines_AreNewestStartFirst", () => {
    const lines = runListLines(
      [attempt({ number: 1, runId: "r-1", startedAt: "2026-09-04T10:00:00Z" }), attempt({ number: 2, runId: "r-2", startedAt: "2026-09-04T12:00:00Z" })],
      [derivation({ startedAt: "2026-09-04T11:00:00Z" })],
    );
    expect(lines.map((l) => l.runId)).toEqual(["r-2", "d-1", "r-1"]);
  });

  // 2026-11-17-3435b
  it("runListLines_DrawMergeGreenRuns_AndNoOtherOtherRun", () => {
    const merge = { runId: "m-1", pipeline: "merge-green", status: "success", startedAt: "2026-09-04T13:00:00Z", finishedAt: "2026-09-04T13:00:20Z", cost: 0, neverStarted: false };
    expect(mergeListLine(merge)).toMatchObject({ who: "Merge", outcome: "done", tone: "ok", href: "/jobs/m-1", attempt: null });
    const lines = runListLines([attempt({ startedAt: "2026-09-04T10:00:00Z" })], [], [merge, { ...merge, runId: "x-1", pipeline: "retarget" }]);
    expect(lines.map((l) => l.runId)).toEqual(["m-1", "r-1"]);
  });

  // 2026-11-18-3863f
  it("runListLines_DrawDeployRuns", () => {
    const deploy = { runId: "dp-1", pipeline: "deploy", status: "success", startedAt: "2026-09-04T14:00:00Z", finishedAt: "2026-09-04T14:00:30Z", cost: 0, neverStarted: false };
    expect(deployListLine(deploy)).toMatchObject({ who: "Deploy", outcome: "delivered", tone: "ok", href: "/jobs/dp-1" });
    const lines = runListLines([attempt({ startedAt: "2026-09-04T10:00:00Z" })], [], [deploy]);
    expect(lines.map((l) => l.runId)).toEqual(["dp-1", "r-1"]);
  });

  it("RunListLines_NewestAttempt_CarriesTheNextStep_AnOlderOnePointsAtTheLatest", () => {
    const one = attempt({ number: 1, runId: "r-1", status: "failed", summary: "Pod did not start" });
    const two = attempt({ number: 2, runId: "r-2", status: "failed", summary: "Pod did not start", startedAt: "2026-09-04T15:00:00Z" });
    const move = { tone: "bad", title: "Stopped", text: "", detail: null, act: null, why: true, question: false, next: "Reply below" } as const;
    const [newest, older] = runListLines([one, two], [], [], { move, latest: two, canRetry: true });
    expect(newest.text).toBe("→ Reply below.");
    expect(older.text).toBe("→ See run 2, the latest.");
  });
});
