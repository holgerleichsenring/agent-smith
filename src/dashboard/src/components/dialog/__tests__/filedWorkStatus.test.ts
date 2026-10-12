import { describe, it, expect } from "vitest";
import {
  isSpecTerminal,
  markClass,
  specStatusWords,
  pullRequestWords,
  runStatusTone,
} from "../filedWorkStatus";

// 2026-09-17-042ef: the words and the tones a state reaches the page in. Every branch is
// pinned here rather than through a render, because a render can only reach the states the
// fixture happens to carry — and the two that matter most on this page, a pull request that
// could not be opened and a run waiting on a person, are exactly the ones no fixture had.

describe("specStatusWords", () => {
  it("SpecStatus_TheFiveTheProjectionWrites_AreEnglishAndOnlyTheStoppedOnesAlarm", () => {
    expect(specStatusWords("not_started")).toEqual({ word: "not started", tone: "" });
    expect(specStatusWords("in_progress")).toEqual({ word: "running", tone: "" });
    expect(specStatusWords("done")).toEqual({ word: "done", tone: "" });
    expect(specStatusWords("failed")).toEqual({ word: "failed", tone: "bad" });
    expect(specStatusWords("handed_back")).toEqual({ word: "handed back", tone: "warn" });
  });

  // A status nobody has named yet is shown, not hidden and not guessed into a state: the
  // underscores open and the tone stays calm, because an unknown word is not an alarm.
  it("SpecStatus_AWordTheMapDoesNotCarry_IsOpenedRatherThanShownRaw", () => {
    expect(specStatusWords("some_new_state")).toEqual({ word: "some new state", tone: "" });
  });
});

describe("isSpecTerminal", () => {
  it("SpecTerminal_TheThreeStoppedStates_AreTerminalAndTheOthersAreNot", () => {
    expect(["done", "failed", "handed_back"].map(isSpecTerminal)).toEqual([true, true, true]);
    expect(["not_started", "in_progress", "invented"].map(isSpecTerminal))
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
