import { describe, it, expect } from "vitest";
import { translator } from "@/lib/i18n/translate";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import { passHead, specPasses } from "../specPasses";

// 2026-11-24-0fbfa: the pass a master step is, read off the step that queued it and the pass ends the run recorded.

function row(stepIndex: number, commandName: string, resultMessage: string | null = null): RunStepRow {
  return {
    stepIndex, stepName: commandName, displayName: null, commandName, specId: "s", status: "success", durationSeconds: 1,
    resultMessage, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
  };
}

function ended(step: number, openedBecause: string, endedBecause: string): RunEvent {
  return {
    runId: "r", type: EventType.MasterPassEnded, timestamp: "t", originStepIndex: step, specId: "s", ordinal: 1, passInStep: 0,
    openedBecause, endedBecause, verdictEmitted: false, gateLine: "", actionablePending: 0, unbackedDone: 0,
  };
}

const t = translator("en");

describe("specPasses", () => {
  it("specPasses_OneMasterStep_NumbersNothing", () => {
    expect(specPasses([row(1, "AgenticMaster"), row(2, "CommitSpecWork")], []).size).toBe(0);
  });

  it("specPasses_MasterAfterCommit_IsACorrectionWithTheCommitsReason", () => {
    const passes = specPasses([row(1, "AgenticMaster"), row(2, "CommitSpecWork", "1 unit(s) over"), row(3, "AgenticMaster")], []);

    expect(passes.get(1)).toEqual({ number: 1, opened: null, because: null, ended: null });
    expect(passes.get(3)).toEqual({ number: 2, opened: "structure-correction", because: "1 unit(s) over", ended: null });
  });

  it("specPasses_RecordedEnds_OutrankTheQueuingCommand", () => {
    const passes = specPasses([row(1, "AgenticMaster"), row(2, "VerifySpec", "red"), row(3, "AgenticMaster")],
      [ended(1, "first", "nothing-left"), ended(3, "repair", "steps-open"), ended(3, "unfinished", "nothing-left")]);

    expect(passes.get(1)?.ended).toBe("nothing-left");
    expect(passes.get(3)).toEqual({ number: 2, opened: "repair", because: "red", ended: "nothing-left" });
  });

  it("specPasses_UnknownQueuingStep_IsAnotherPass", () => {
    const passes = specPasses([row(1, "AgenticMaster"), row(2, "SomethingElse"), row(3, "AgenticMaster")], []);

    expect(passHead(t, passes.get(3)!)).toBe("Pass 2 of this spec · another pass");
  });

  it("passHead_EndedLaterPass_NamesKindAndEnd", () => {
    expect(passHead(t, { number: 2, opened: "after-answer", because: null, ended: "question" }))
      .toBe("Pass 2 of this spec · after the answer · ended: asked a question");
  });
});
