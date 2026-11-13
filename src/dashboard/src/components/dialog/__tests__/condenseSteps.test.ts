import { describe, expect, it } from "vitest";
import type { SpecDialogActivityPush, SpecDialogActivityStep } from "@/types/spec-dialog";
import { condenseSteps, type RawStep } from "../condenseSteps";

// 2026-11-12-92e9c: one condenser for the steps pushed while a turn runs and the pages fetched from its run.

const raw: RawStep[] = [
  { seq: 1, kind: "model", name: "m", detail: "Looking at the import path first." },
  { seq: 2, kind: "tool", name: "read_file", detail: "a.cs" },
  { seq: 3, kind: "model", name: "m", detail: null },
  { seq: 4, kind: "tool", name: "grep", detail: "Store" },
  { seq: 5, kind: "tool", name: "read_file", detail: "b.cs" },
  { seq: 6, kind: "tool", name: "write_spec", detail: "2026-11-12-0001" },
  { seq: 7, kind: "model", name: "m", detail: "  " },
  { seq: 8, kind: "tool", name: "find_files", detail: "*.ts" },
  { seq: 9, kind: "reviewing", name: null, detail: null },
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
      ["write", "write_spec 2026-11-12-0001"],
      ["reads", "1 search"],
      ["step", "reviewing its proposal"],
    ]);
    expect(lines.map((line) => line.key)).toEqual([1, 2, 6, 8, 9]);
    expect(condenseSteps(fetched)).toEqual(lines);
  });
});
