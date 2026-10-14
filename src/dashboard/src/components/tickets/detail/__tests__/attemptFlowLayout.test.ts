import { describe, expect, it } from "vitest";
import { layoutAttemptFlow, type AttemptFlowInput } from "../attemptFlowLayout";
import { questionJoin } from "../questionJoin";
import { attempt, entry, pass, passes, series, spec } from "./detailFixtures";

// 2026-10-13-9a59q: the graph's shape, without a DOM.

function input(over: Partial<AttemptFlowInput> = {}): AttemptFlowInput {
  const a = over.attempt ?? attempt();
  return { attempt: a, nextAttempt: null, series: series(a.runId, [spec()]), passes: passes(a.runId, { s1: [pass()] }), entries: [], ...over };
}

const kinds = (layout: ReturnType<typeof layoutAttemptFlow>) => layout.nodes.map((n) => n.kind);

describe("layoutAttemptFlow", () => {
  it("HandedBackAttempt_EndsAtHandBack_WithNoGateAndNoSeries", () => {
    const a = attempt({ handedBack: true, status: "success", revision: null });
    const layout = layoutAttemptFlow(input({
      attempt: a, nextAttempt: attempt({ number: 2, runId: "r-2" }),
      series: series("r-1", [], { handback: { case: "RequirementsContradictRepository", runId: "r-1", attempt: 1, at: "x" }, gate: { status: "skipped", specCount: 0, runId: "r-1" } }),
    }), "unrolled");
    expect(kinds(layout)).toEqual(["ticket", "derive", "handback", "tail"]);
    expect(layout.texts.some((t) => t.text.startsWith("SERIES"))).toBe(false);
    expect(layout.nodes.find((n) => n.kind === "handback")?.metric).toBe("contradiction");
    expect(layout.nodes.find((n) => n.kind === "tail")).toMatchObject({ label: "ENDS", state: "person" });
    expect(layout.texts.some((t) => t.text === "→ attempt 2")).toBe(true);
  });

  it("CarriedSpecs_AreOneDashedRowEach_PricedZero_BeforeTheSpecsThisAttemptRuns", () => {
    const a = attempt({ runId: "r-3", number: 3, revision: { number: 3, cause: "c", seriesId: "x", carriedIds: ["s1", "s2"], approvalAt: null } });
    const layout = layoutAttemptFlow(input({
      attempt: a,
      series: series("r-3", [
        spec({ specId: "s1", ordinal: 1, state: "carried", executedByAttempt: 2 }),
        spec({ specId: "s2", ordinal: 2, state: "carried", executedByAttempt: 2 }),
        spec({ specId: "s3", ordinal: 3, state: "done" }),
      ]),
      passes: passes("r-3", { s3: [pass()] }),
    }), "unrolled");
    const specs = layout.nodes.filter((n) => n.kind === "spec");
    expect(specs.map((n) => [n.specId, n.state, n.r])).toEqual([["s1", "carried", 18], ["s2", "carried", 18], ["s3", "done", 26]]);
    expect(specs[0].y).toBeLessThan(specs[2].y);
    expect(layout.texts.filter((t) => t.cls === "carrytxt").map((t) => t.text)).toEqual([
      "executed in A2 · carried · $0", "executed in A2 · carried · $0",
    ]);
    expect(layout.nodes.filter((n) => n.kind === "build").map((n) => n.specId)).toEqual(["s3"]);
  });

  it("TwoPasses_DrawTwoLanesNumberedByPassInSpec_AndOneReturnEdgeLabelledWithTheReengageReason", () => {
    const layout = layoutAttemptFlow(input({
      passes: passes("r-1", { s1: [
        pass({ ordinal: 4, passInSpec: 1, endedBecause: "re-engaged", reengageReason: "red-with-open-steps", criteriaMet: 0 }),
        pass({ ordinal: 7, passInSpec: 2, openedBecause: "red-with-open-steps" }),
      ] }),
    }), "unrolled");
    expect(layout.lanes).toHaveLength(2);
    expect(layout.texts.filter((t) => t.cls === "passlab").map((t) => t.text)).toEqual(["PASS 1 OF 2", "PASS 2 OF 2"]);
    expect(layout.edges.filter((e) => e.cls === "back")).toHaveLength(1);
    expect(layout.texts.find((t) => t.cls === "elab")?.text).toBe("↺ verdict red with open steps");
    expect(layout.nodes.filter((n) => n.kind === "verify").map((n) => n.state)).toEqual(["bad", "done"]);
  });

  it("Folded_DrawsTheLastPassOnly_WithACountBadgeOnBuildAndVerify", () => {
    const layout = layoutAttemptFlow(input({
      passes: passes("r-1", { s1: [pass({ ordinal: 1, endedBecause: "re-engaged" }), pass({ ordinal: 2, passInSpec: 2 })] }),
    }), "folded");
    const lane = layout.nodes.filter((n) => n.kind === "build" || n.kind === "verify");
    expect(lane.map((n) => [n.kind, n.badge, n.pass?.passInSpec])).toEqual([["build", "×2", 2], ["verify", "×2", 2]]);
    expect(layout.edges.some((e) => e.cls === "back")).toBe(false);
    expect(layout.lanes).toHaveLength(0);
  });

  it("NotStartedSpecOfTheCut_IsDrawnIdle", () => {
    const a = attempt({ status: "running", finishedAt: null });
    const layout = layoutAttemptFlow(input({
      attempt: a,
      series: series("r-1", [spec({ state: "in_progress" }), spec({ specId: "s2", ordinal: 2, state: "not_started" })]),
      passes: passes("r-1", { s1: [pass({ endedBecause: "unknown", criteriaMet: null, criteriaTotal: null, verdictEmitted: null })] }),
    }), "unrolled");
    const s2 = layout.nodes.filter((n) => n.specId === "s2");
    expect(s2.map((n) => [n.kind, n.state])).toEqual([["spec", "idle"], ["build", "idle"], ["verify", "idle"]]);
    expect(s2[0].metric).toBe("cut, not started");
    expect(layout.nodes.find((n) => n.id === "b-s1-1")?.state).toBe("live");
  });

  it("Question_HangsOffTheBuildOfThePassThatParked", () => {
    const layout = layoutAttemptFlow(input({
      attempt: attempt({ status: "waiting_for_input", finishedAt: null }),
      series: series("r-1", [spec({ state: "in_progress" })]),
      passes: passes("r-1", { s1: [pass({ ordinal: 3, endedBecause: "question", verdictEmitted: null, criteriaMet: null, criteriaTotal: null })] }),
      entries: [entry({ id: "question:r-1:q1" })],
    }), "unrolled");
    const ask = layout.nodes.find((n) => n.kind === "question");
    const build = layout.nodes.find((n) => n.kind === "build");
    expect(ask).toMatchObject({ id: "ask-s1-1", state: "person", x: 685 });
    expect(ask!.y).toBe(build!.y - 70);
    expect(ask!.pass?.ordinal).toBe(3);
  });

  it("LiveAttemptWithoutACut_DrawsTicketAndDeriveWorking_NotTheFallback", () => {
    const layout = layoutAttemptFlow(input({
      attempt: attempt({ status: "running", finishedAt: null, revision: null }),
      series: series("", [], { runId: null, gate: null, recorded: false }),
      passes: passes("r-1", {}),
    }), "unrolled");
    expect(layout.fallback).toBe(false);
    expect(layout.nodes.map((n) => [n.kind, n.state])).toEqual([["ticket", "done"], ["derive", "live"]]);
  });

  it("TerminalAttemptWithNoRevisionAndNoCut_IsTheFallback", () => {
    const layout = layoutAttemptFlow(input({
      attempt: attempt({ revision: null, status: "failed" }),
      series: series("", [], { runId: null, gate: null, recorded: false }),
    }), "unrolled");
    expect(layout.fallback).toBe(true);
    expect(layout.nodes).toHaveLength(0);
  });

  it("TerminalAttemptWithARunRecordRead_IsTheFallback", () => {
    const layout = layoutAttemptFlow(input({
      attempt: attempt({ revision: null, status: "failed" }),
      series: series("r-1", [spec({ fileStem: null })], { gate: null, source: "run-record" }),
    }), "unrolled");
    expect(layout.fallback).toBe(true);
  });

  it("DeriveBadge_CountsTheAttemptsRefusedCutReviews", () => {
    const review = (deliverable: string, id: string) =>
      entry({ id, source: "spec-cut", kind: "cut-review", detail: { attempt: 1, deliverable, findings: [] } });
    const layout = layoutAttemptFlow(input({ entries: [review("refused", "a"), review("accepted", "b")] }), "unrolled");
    expect(layout.nodes.find((n) => n.kind === "derive")?.badge).toBe("1✗");
  });
});

describe("questionJoin", () => {
  const parked = (ordinal: number) => pass({ ordinal, endedBecause: "question" });

  it("PairsTheKthQuestionEndedPassWithTheKthQuestionEntry", () => {
    const runPasses = passes("r-1", { s2: [parked(5)], s1: [parked(2), pass({ ordinal: 3 })] });
    const entries = [
      entry({ id: "question:r-1:a", text: "first?", at: "2026-09-04T10:00:00Z" }),
      entry({ id: "question:r-1:a:answer", kind: "answer", role: "person", author: "h.operator", text: "yes", at: "2026-09-04T13:12:00Z" }),
      entry({ id: "question:r-1:b", text: "second?" }),
    ];
    const join = questionJoin(runPasses, entries);
    expect(join.matched).toBe(true);
    expect(join.pairs.get(2)?.question.text).toBe("first?");
    expect(join.pairs.get(2)?.answer?.author).toBe("h.operator");
    expect(join.pairs.get(2)?.parkedMs).toBe((3 * 60 + 12) * 60000);
    expect(join.pairs.get(5)?.question.text).toBe("second?");
    expect(join.pairs.get(5)?.answer).toBeNull();
  });

  it("UnequalCounts_PairNothing", () => {
    const join = questionJoin(passes("r-1", { s1: [parked(2)] }), [entry({ id: "q1" }), entry({ id: "q2" })]);
    expect(join.matched).toBe(false);
    expect(join.pairs.size).toBe(0);
  });
});
