import { describe, expect, it } from "vitest";
import type { SpecDialogActivityPush, SpecDialogActivityStep } from "@/types/spec-dialog";
import { condenseSteps, inFlight, type RawStep } from "../condenseSteps";

// 2026-11-12-92e9c: one condenser for the steps pushed while a turn runs and the pages fetched from its run.

const raw: RawStep[] = [
  { seq: 1, kind: "model", name: "m", detail: "Looking at the import path first." },
  { seq: 2, kind: "tool", name: "read_file", detail: "a.cs" },
  { seq: 3, kind: "model", name: "m", detail: null },
  { seq: 4, kind: "tool", name: "grep", detail: "Store" },
  { seq: 5, kind: "tool", name: "read_file", detail: "b.cs" },
  { seq: 6, kind: "tool", name: "write_file", detail: "draft:specs/p0001.yaml" },
  { seq: 7, kind: "model", name: "m", detail: "  " },
  { seq: 8, kind: "tool", name: "find_files", detail: "*.ts" },
  { seq: 9, kind: "revising", name: null, detail: null },
];

const live: SpecDialogActivityPush[] = raw.map((step) => ({
  dialogId: "d-1", kind: step.kind as SpecDialogActivityPush["kind"], name: step.name ?? null, detail: step.detail ?? null,
  at: "2026-11-12T10:00:00Z", seq: step.seq, turnStartedAt: "2026-11-12T10:00:00Z",
}));
const fetched: SpecDialogActivityStep[] = raw.map((step) => ({ ...step, timestamp: "2026-11-12T10:00:00Z" }));

describe("condenseSteps", () => {
  it("CountsReadBursts_KeepsWritesAndNarration_SameForLiveAndFetched", () => {
    const lines = condenseSteps(live);

    expect(lines.map((line) => [line.kind, line.text])).toEqual([
      ["said", "Looking at the import path first."],
      ["reads", "2 reads · 1 search"],
      ["write", "write_file draft:specs/p0001.yaml"],
      ["reads", "1 search"],
      ["step", "revising its answer"],
    ]);
    expect(lines.map((line) => line.key)).toEqual([1, 2, 6, 8, 9]);
    expect(condenseSteps(fetched)).toEqual(lines);
  });

  // 2026-11-15-06fc: the draft is files; deleting one changes the draft like writing or editing one.
  it("DraftFileTools_AreWriteLines", () => {
    const lines = condenseSteps([
      { seq: 1, kind: "tool", name: "edit_file", detail: "draft:specs/p0001.yaml" },
      { seq: 2, kind: "tool", name: "delete_file", detail: "draft:specs/p0002.yaml" },
    ]);

    expect(lines.map((line) => [line.kind, line.text])).toEqual([
      ["write", "edit_file draft:specs/p0001.yaml"],
      ["write", "delete_file draft:specs/p0002.yaml"],
    ]);
  });

  // 2026-11-13-3dc8: a sub-agent's or reviewer's steps arrive as "Name › …".
  it("PrefixedSubAgentReads_JoinBurst", () => {
    const lines = condenseSteps([
      { seq: 1, kind: "tool", name: "read_file", detail: "a.cs" },
      { seq: 2, kind: "tool", name: "RepoScout › read_file", detail: "b.cs" },
      { seq: 3, kind: "tool", name: "Review › grep", detail: "Store" },
    ]);

    expect(lines.map((line) => line.text)).toEqual(["2 reads · 1 search"]);
  });

  it("PrefixedModelStep_NotALine", () => {
    const lines = condenseSteps([
      { seq: 1, kind: "tool", name: "read_file", detail: "a.cs" },
      { seq: 2, kind: "model", name: "Review › stub-model", detail: '{"verdict":"REFUSE","findings":[]}' },
      { seq: 3, kind: "tool", name: "read_file", detail: "b.cs" },
    ]);

    expect(lines.map((line) => line.text)).toEqual(["2 reads"]);
  });

  it("MasterNarration_Kept", () => {
    const lines = condenseSteps([
      { seq: 1, kind: "model", name: "master-model", detail: "Checking the router." },
      { seq: 2, kind: "revising", name: null, detail: null },
      { seq: 3, kind: "model", name: "Review › stub-model", detail: "[]" },
    ]);

    expect(lines.map((line) => [line.kind, line.text])).toEqual([
      ["said", "Checking the router."],
      ["step", "revising its answer"],
    ]);
  });

  // 2026-11-20-24bd: a turn whose run waits for its place on the model shows the gate's sentence as one line; the call
  // that follows the wait reports thinking (the gate sits before it), which the working view shows live — no line.
  it("WaitingForTheModel_IsOneLineWithTheSentence_AndTheCallAfterItThinksLive", () => {
    const sentence = "Waiting for the model: gpt41 runs 1 at a time (agent.max_concurrent_runs)";
    const steps: RawStep[] = [
      { seq: 1, kind: "waiting", name: null, detail: sentence },
      { seq: 2, kind: "thinking", name: "gpt41", detail: null },
    ];

    expect(condenseSteps(steps.slice(0, 1)).map((line) => [line.kind, line.text])).toEqual([["step", sentence]]);
    expect(inFlight(steps.slice(0, 1))).toBeNull();
    // the wait ended: the history line is past tense and the call after it thinks live
    expect(condenseSteps(steps).map((line) => line.text))
      .toEqual(["Waited for the model: gpt41 runs 1 at a time (agent.max_concurrent_runs)"]);
    expect(inFlight(steps)).toEqual({ key: 2, who: null });
  });
});
