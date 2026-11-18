import { describe, expect, it } from "vitest";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunSnapshot } from "@/types/hub-events";
import { initiallyOpen, placeLogSteps, runLogSections, stepsMeta, stepTime } from "../runLogSections";

// 2026-11-17-6d9eb: the log sectioned by work — Got ready, a section per spec, Delivered — with per-section counts.

function row(stepIndex: number, commandName: string, over: Partial<RunStepRow> = {}): RunStepRow {
  return {
    stepIndex, stepName: commandName, displayName: commandName, commandName, status: "success", durationSeconds: 10,
    resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over,
  };
}

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: [], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:00:00Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: null, totalSteps: 12,
    lastEventType: null, costUsd: 0, llmCalls: 0, ticketId: "13", ticketTitle: "Upgrade", agentName: null,
    cancelRequested: false, ...over,
  };
}

const SPEC = "2026-10-07-1891a";
const steps = [
  row(1, "FetchTicket", { durationSeconds: 1.5 }),
  row(2, "AnalyzeCode", { llmCalls: 88, costUsd: 0.08, durationSeconds: 266 }),
  row(3, "DeriveSpec", { durationSeconds: 8.8 }),
  row(4, "CheckSpecPremises", { specId: SPEC, llmCalls: 2, costUsd: 0.02, durationSeconds: 5 }),
  row(5, "AgenticMaster", { specId: SPEC, llmCalls: 168, costUsd: 7.26, durationSeconds: 4510, status: "running" }),
  row(6, "LoadContext", { specId: SPEC, stepClass: "internal" }),
  row(7, "WriteRunResult", { planned: true, status: null, durationSeconds: null }),
  row(8, "CommitAndPR", { planned: true, status: null, durationSeconds: null }),
];

describe("runLogSections", () => {
  it("placeLogSteps_BeforeTheFirstSpec_InASpec_AndDelivered", () => {
    expect(placeLogSteps(steps).map((p) => p.key)).toEqual(["ready", "ready", "ready", `spec:${SPEC}`, `spec:${SPEC}`, `spec:${SPEC}`, "deliver", "deliver"]);
  });

  it("placeLogSteps_ARunWithoutSpecs_ReadsGotReadyTheWorkDelivered", () => {
    const plain = [row(1, "FetchTicket"), row(2, "AgenticExecute"), row(3, "VerifySpec"), row(4, "SomethingNew"), row(5, "CommitAndPR")];
    expect(placeLogSteps(plain).map((p) => p.key)).toEqual(["ready", "work", "work", "work", "deliver"]);
  });

  it("runLogSections_OrderTitlesAndMeta", () => {
    const specs = [{ specId: SPEC, ordinal: 1, title: "upgrade dependencies", status: "in_progress", startedAt: null, endedAt: null, verdict: null, decisions: [], steps: [] }] as RunSpecRow[];
    const sections = runLogSections(steps, specs, snap(), null);
    expect(sections.map((s) => s.id)).toEqual(["ticket", "ready", "plan", `spec:${SPEC}`, "check", "deliver"]);
    expect(sections[0].meta).toBe("13");
    expect(sections[1].meta).toBe("3 steps · 88 llm · $0.08 · 4m 36s");
    const spec = sections[3];
    expect(spec.title).toBe(`Spec ${SPEC} · upgrade dependencies`);
    expect(spec.state).toBe("running");
    expect(spec.meta).toBe("2 of 3 steps done · 170 llm · $7.28 · 1h 15m");
    expect(spec.steps.map((s) => s.story)).toEqual([true, true, false]);
    expect(sections[5].meta).toMatch(/^not reached yet · 0 of 2 steps done/);
    expect(initiallyOpen(sections)).toEqual(new Set([`spec:${SPEC}`]));
  });

  it("runLogSections_AHandedBackSpec_IsRed_AndSaysWhy", () => {
    const specs = [{ specId: SPEC, ordinal: 1, title: "t", status: "handed_back", startedAt: null, endedAt: null, verdict: "premise false", decisions: [], steps: [] }] as RunSpecRow[];
    const spec = runLogSections(steps.slice(0, 4), specs, snap(), null)[3];
    expect(spec.state).toBe("bad");
    expect(spec.meta).toBe("handed back · 1 step · 2 llm · $0.02 · 5s · premise false");
  });

  it("runLogSections_FinishedWithAPullRequest_DeliveredSaysSo", () => {
    const done = runLogSections([row(1, "CommitAndPR", { durationSeconds: 17.1 })], [],
      snap({ status: "success", prUrl: "https://example.test/a/pull/1" }), null);
    expect(done.find((s) => s.id === "deliver")?.meta).toBe("pull request opened · 1 step · 17s");
  });

  it("stepTime_And_stepsMeta", () => {
    expect(stepTime(1.5)).toBe("1.5s");
    expect(stepTime(266)).toBe("4m 26s");
    expect(stepTime(null)).toBe("");
    expect(stepsMeta([row(1, "A")])).toBe("1 step · 10s");
  });
});
