import { describe, it, expect } from "vitest";
import { runStatusWord, toNodeStatus } from "../runStatus";

describe("toNodeStatus", () => {
  it("toNodeStatus_KnownStatuses_MapToNodeStatus", () => {
    expect(toNodeStatus("success")).toBe("ok");
    expect(toNodeStatus("failed")).toBe("fail");
    expect(toNodeStatus("error")).toBe("fail");
    expect(toNodeStatus("running")).toBe("run");
    // p0259: cancelled is its own node status, not "fail" and not "wait".
    expect(toNodeStatus("cancelled")).toBe("cancel");
    expect(toNodeStatus("CANCELLED")).toBe("cancel");
  });

  it("toNodeStatus_UnknownStatus_MapsToWait", () => {
    expect(toNodeStatus("pending")).toBe("wait");
    expect(toNodeStatus("")).toBe("wait");
  });

  it("toNodeStatus_Queued_MapsToItsOwnTone", () => {
    // p0320d: queued is a first-class amber state, distinct from a stalled "wait".
    expect(toNodeStatus("queued")).toBe("queued");
  });

  it("toNodeStatus_Shortfall_MapsToItsOwnIdentity", () => {
    // p0439: delivered with a shortfall — not "ok" with a footnote, not "fail".
    expect(toNodeStatus("shortfall")).toBe("shortfall");
  });

  it("toNodeStatus_WaitingForInput_MapsToInput", () => {
    // p0327: parked on a question — waiting for the OPERATOR, not capacity.
    expect(toNodeStatus("waiting_for_input")).toBe("input");
  });
});

// 2026-09-17-042ef: the words moved here out of RunCard so the runs list and the Work it out
// page read one map, and arrived with no test of their own.
describe("runStatusWord", () => {
  it("RunStatusWord_TheEightTheProductWrites_ReadAsWordsNotColumnValues", () => {
    expect(runStatusWord("running")).toBe("running");
    expect(runStatusWord("success")).toBe("success");
    expect(runStatusWord("shortfall")).toBe("done, with a shortfall");
    expect(runStatusWord("failed")).toBe("failed");
    expect(runStatusWord("error")).toBe("error");
    expect(runStatusWord("cancelled")).toBe("cancelled");
    expect(runStatusWord("queued")).toBe("queued — waiting for capacity");
    expect(runStatusWord("waiting_for_input")).toBe("waiting for your input");
  });

  it("RunStatusWord_TheColumnsCase_DoesNotDecideTheWord", () => {
    expect(runStatusWord("RUNNING")).toBe("running");
  });

  // The behaviour the move changed, pinned rather than left to be noticed later: RunCard used
  // to print an unrecognised status verbatim. It now opens its underscores and lowercases it,
  // which is what the Work it out page needs and what the runs list is better for. RunStatuses
  // covers all eight, so no live payload reaches this branch — which is exactly why it would
  // have gone unnoticed.
  it("RunStatusWord_AStatusTheMapDoesNotCarry_IsOpenedRatherThanPrintedRaw", () => {
    expect(runStatusWord("some_new_state")).toBe("some new state");
  });

  // An ABSENT status must not become a word. RunCard renders "unknown" for it and that guard
  // lives there, so this returns the empty string the guard reads.
  it("RunStatusWord_NoStatusAtAll_IsEmptySoTheCallerCanSaySoItself", () => {
    expect(runStatusWord(null)).toBe("");
    expect(runStatusWord(undefined)).toBe("");
    expect(runStatusWord("")).toBe("");
  });
});
