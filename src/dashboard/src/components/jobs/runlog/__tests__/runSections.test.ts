import { describe, expect, it } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import { checkOf, commandKey, runSections, sectionDuration } from "../runSections";

// 2026-11-16-6877b: which section a step reads under, and what a section says about itself.

let index = 0;
function step(commandName: string, over: Partial<RunStepRow> = {}): RunStepRow {
  index += 1;
  return {
    stepIndex: index, stepName: commandName, displayName: commandName, commandName, status: "success",
    durationSeconds: 10, resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over,
  };
}

const placed = (steps: RunStepRow[]) => Object.fromEntries(
  runSections(steps, [], "pending", "").map((s) => [s.id, s.lines.map((l) => l.text)]));

describe("runSections", () => {
  it("runSections_EachCommandReadsUnderItsSection", () => {
    const sections = placed([
      step("FetchTicketCommand"), step("CheckoutSourceCommand"), step("DeriveSpecCommand"), step("SelectSpecCommand"),
      step("AgenticMasterCommand:server"), step("VerifySpecCommand"), step("ReviewSpecDiffCommand"), step("CommitAndPRCommand"),
    ]);
    expect(sections).toEqual({
      ready: ["FetchTicketCommand", "CheckoutSourceCommand"], spec: ["DeriveSpecCommand", "SelectSpecCommand"],
      build: ["AgenticMasterCommand:server"], verify: ["VerifySpecCommand"], review: ["ReviewSpecDiffCommand"],
      check: [], deliver: ["CommitAndPRCommand"],
    });
  });

  it("runSections_ABuildAfterAReviewOfTheSameSpec_IsTheFixPass_ANewSpecStartsOver", () => {
    const sections = placed([
      step("SelectSpec", { specId: "s1" }), step("AgenticMaster", { specId: "s1" }), step("ReviewSpecDiff", { specId: "s1" }),
      step("AgenticMaster", { specId: "s1", displayName: "fix" }), step("VerifySpec", { specId: "s1", displayName: "reverify" }),
      step("SelectSpec", { specId: "s2", displayName: "next" }), step("AgenticMaster", { specId: "s2", displayName: "build s2" }),
    ]);
    expect(sections.review).toEqual(["ReviewSpecDiff", "fix", "reverify"]);
    expect(sections.build).toEqual(["AgenticMaster", "build s2"]);
  });

  // 2026-11-17-b6e4b: a file run delivers its tickets; a security scan's config preflight is getting ready.
  it("runSections_FileTickets_ReadsUnderDelivered", () => {
    const sections = placed([step("PlanFilingCommand"), step("FileTicketsCommand")]);
    expect(sections.ready).toEqual(["PlanFilingCommand"]);
    expect(sections.deliver).toEqual(["FileTicketsCommand"]);
  });

  it("runSections_RunConfigPreflightAfterRatifyScanContract_ReadsUnderGotReady", () => {
    const sections = placed([step("RatifyScanContractCommand"), step("RunConfigPreflightCommand")]);
    expect(sections.spec).toEqual(["RatifyScanContractCommand"]);
    expect(sections.ready).toEqual(["RunConfigPreflightCommand"]);
  });

  it("runSections_AnUnknownCommand_JoinsTheSectionBeforeIt_TheFirstJoinsGotReady", () => {
    const sections = placed([step("SomethingNew"), step("DeriveSpec"), step("AnotherNewOne")]);
    expect(sections.ready).toEqual(["SomethingNew"]);
    expect(sections.spec).toEqual(["DeriveSpec", "AnotherNewOne"]);
  });

  it("runSections_State_FailedOverRunningOverFinding_UnreachedIsPending", () => {
    const sections = runSections([
      step("FetchTicket"), step("DeriveSpec", { hasFinding: true }), step("AgenticMaster", { status: "running" }),
      step("VerifySpec", { status: "failed", resultMessage: "dotnet test: 2 failed\nmore" }),
      step("CommitAndPR", { planned: true, status: null }),
    ], [], "pending", "no verdict recorded");
    const by = Object.fromEntries(sections.map((s) => [s.id, s]));
    expect(by.ready.state).toBe("done");
    expect(by.spec.state).toBe("warn");
    expect(by.build.state).toBe("running");
    expect(by.verify).toMatchObject({ state: "bad", summary: "dotnet test: 2 failed", seconds: 10 });
    expect(by.deliver).toMatchObject({ state: "pending", summary: "not reached yet" });
    expect(by.review).toMatchObject({ state: "pending", summary: "nothing ran here" });
    expect(by.check).toMatchObject({ state: "pending", summary: "no verdict recorded" });
  });

  it("runSections_ALineCarriesItsStepStartedTime_WhenTheEventIsHeld", () => {
    const fetch = step("FetchTicket");
    const events = [{ runId: "r", type: EventType.StepStarted, timestamp: "2026-10-13T06:05:48Z", stepIndex: fetch.stepIndex, stepName: "x", totalSteps: 1, displayName: null } as RunEvent];
    const [ready] = runSections([fetch, step("CheckoutSource")], events, "pending", "");
    expect(ready.lines[0].at).toMatch(/^\d\d:05:48$/);
    expect(ready.lines[1].at).toBeNull();
  });

  it("commandKey_StripsTheSuffixAndTheParameter", () => {
    expect(commandKey(step("AgenticMasterCommand:server"))).toBe("AgenticMaster");
    expect(commandKey(step("FetchTicket"))).toBe("FetchTicket");
  });

  it("checkOf_RedOnUnmet_AmberOnUnproven_GreenWhenEveryOneIsMet", () => {
    const c = (status: "met" | "unmet" | "unproven") => ({ text: "x", status, reason: null });
    expect(checkOf(null)).toEqual({ state: "pending", summary: "no verdict recorded" });
    expect(checkOf({ criteria: [c("met"), c("met")], outcome: null, ratifiedBy: null })).toEqual({ state: "done", summary: "2 of 2 done criteria met" });
    expect(checkOf({ criteria: [c("met"), c("unproven")], outcome: null, ratifiedBy: null }).state).toBe("warn");
    expect(checkOf({ criteria: [c("unproven"), c("unmet")], outcome: null, ratifiedBy: null })).toEqual({ state: "bad", summary: "0 of 2 done criteria met" });
  });

  it("sectionDuration_ReadsLikeTheMock", () => {
    expect(sectionDuration(0)).toBe("");
    expect(sectionDuration(28)).toBe("28 s");
    expect(sectionDuration(67)).toBe("1 min 07 s");
    expect(sectionDuration(3900)).toBe("1 h 05 min");
  });
});
