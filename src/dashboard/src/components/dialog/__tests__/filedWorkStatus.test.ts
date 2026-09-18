import { describe, it, expect } from "vitest";
import {
  isPhaseTerminal,
  markClass,
  phaseStatusWords,
  pullRequestWords,
  runStatusTone,
} from "../filedWorkStatus";

// 2026-09-17-042ef: the words and the tones a state reaches the page in. Every branch is
// pinned here rather than through a render, because a render can only reach the states the
// fixture happens to carry — and the two that matter most on this page, a pull request that
// could not be opened and a run waiting on a person, are exactly the ones no fixture had.

describe("phaseStatusWords", () => {
  it("PhaseStatus_TheFiveTheProjectionWrites_AreEnglishAndOnlyTheStoppedOnesAlarm", () => {
    expect(phaseStatusWords("not_started")).toEqual({ word: "not started", tone: "" });
    expect(phaseStatusWords("in_progress")).toEqual({ word: "running", tone: "" });
    expect(phaseStatusWords("done")).toEqual({ word: "done", tone: "" });
    expect(phaseStatusWords("failed")).toEqual({ word: "failed", tone: "bad" });
    expect(phaseStatusWords("handed_back")).toEqual({ word: "handed back", tone: "warn" });
  });

  // A status nobody has named yet is shown, not hidden and not guessed into a state: the
  // underscores open and the tone stays calm, because an unknown word is not an alarm.
  it("PhaseStatus_AWordTheMapDoesNotCarry_IsOpenedRatherThanShownRaw", () => {
    expect(phaseStatusWords("some_new_state")).toEqual({ word: "some new state", tone: "" });
  });
});

describe("isPhaseTerminal", () => {
  it("PhaseTerminal_TheThreeStoppedStates_AreTerminalAndTheOthersAreNot", () => {
    expect(["done", "failed", "handed_back"].map(isPhaseTerminal)).toEqual([true, true, true]);
    expect(["not_started", "in_progress", "invented"].map(isPhaseTerminal))
      .toEqual([false, false, false]);
  });
});

describe("pullRequestWords", () => {
  it("PullRequestStatus_TheThreeTheRunRecords_AreEnglishAndOnlyAFailureAlarms", () => {
    expect(pullRequestWords("opened")).toEqual({ word: "opened", tone: "" });
    expect(pullRequestWords("no_changes")).toEqual({ word: "no changes", tone: "" });
    expect(pullRequestWords("failed")).toEqual({ word: "could not be opened", tone: "bad" });
  });

  it("PullRequestStatus_AWordTheMapDoesNotCarry_IsOpenedRatherThanShownRaw", () => {
    expect(pullRequestWords("half_open")).toEqual({ word: "half open", tone: "" });
  });
});

describe("runStatusTone", () => {
  it("RunStatus_AFailure_IsBad_AndSomebodysTurn_IsWarn_AndTheRestAreCalm", () => {
    expect(["failed", "error"].map(runStatusTone)).toEqual(["bad", "bad"]);
    expect(["queued", "waiting_for_input"].map(runStatusTone)).toEqual(["warn", "warn"]);
    expect(["running", "success", "cancelled", "shortfall"].map(runStatusTone))
      .toEqual(["", "", "", ""]);
  });

  it("RunStatus_TheColumnsCase_DoesNotDecideTheTone", () => {
    expect(runStatusTone("FAILED")).toBe("bad");
  });
});

describe("markClass", () => {
  it("MarkClass_ACalmTone_IsThePlainMark_AndAnAlarmAppendsItsModifier", () => {
    expect(markClass("")).toBe("ec-mark");
    expect(markClass("warn")).toBe("ec-mark warn");
    expect(markClass("bad")).toBe("ec-mark bad");
  });
});
