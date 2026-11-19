import { describe, expect, it } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import { checkOf, commandKey, commandSection, sectionState, stepState } from "../runSections";
import { runLogSections } from "../runLogSections";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-16-6877b: which section a step's command reads under, and the states a step and a section read in.
// 2026-11-18-d6d2e: the seven fixed sections are gone; the command table and the states stay.

let index = 0;
function step(commandName: string, over: Partial<RunStepRow> = {}): RunStepRow {
  index += 1;
  return {
    stepIndex: index, stepName: commandName, displayName: commandName, commandName, status: "success",
    durationSeconds: 10, resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over,
  };
}

describe("runSections", () => {
  it("commandSection_EachCommandNamesItsSection", () => {
    const sections = ["FetchTicketCommand", "DeriveSpecCommand", "AgenticMasterCommand:server", "VerifySpecCommand",
      "ReviewSpecDiffCommand", "CommitAndPRCommand"].map((c) => commandSection(step(c)));
    expect(sections).toEqual(["ready", "spec", "build", "verify", "review", "deliver"]);
  });

  // 2026-11-17-b6e4b: a file run delivers its tickets; a security scan's config preflight is getting ready.
  it("commandSection_FileTicketsDelivers_PlanFilingAndRunConfigPreflightGetReady", () => {
    expect(commandSection(step("FileTicketsCommand"))).toBe("deliver");
    expect(commandSection(step("PlanFilingCommand"))).toBe("ready");
    expect(commandSection(step("RunConfigPreflightCommand"))).toBe("ready");
  });

  it("commandSection_AnUnknownCommand_NamesNone", () => {
    expect(commandSection(step("SomethingNew"))).toBeUndefined();
  });

  it("sectionState_FailedOverRunningOverFinding_UnreachedIsPending", () => {
    expect(stepState(step("DeriveSpec", { hasFinding: true }))).toBe("warn");
    expect(sectionState([step("FetchTicket"), step("DeriveSpec", { hasFinding: true }), step("AgenticMaster", { status: "running" })])).toBe("running");
    expect(sectionState([step("AgenticMaster", { status: "running" }), step("VerifySpec", { status: "failed" })])).toBe("bad");
    expect(sectionState([step("CommitAndPR", { planned: true, status: null })])).toBe("pending");
    expect(sectionState([step("FetchTicket")])).toBe("done");
  });

  it("commandKey_StripsTheSuffixAndTheParameter", () => {
    expect(commandKey(step("AgenticMasterCommand:server"))).toBe("AgenticMaster");
    expect(commandKey(step("FetchTicket"))).toBe("FetchTicket");
  });

  // A run stored before 2026-11-18-98fa keeps its recorded verdicts and their count.
  it("checkOf_RecordedVerdicts_RedOnUnmet_AmberOnUnproven_GreenWhenEveryOneIsMet", () => {
    const c = (status: "met" | "unmet" | "unproven") => ({ text: "x", status, reason: null });
    expect(checkOf({ criteria: [c("met"), c("met")], outcome: null, ratifiedBy: null }))
      .toEqual({ state: "done", summary: "2 of 2 done criteria met", title: "Checked against the spec" });
    expect(checkOf({ criteria: [c("met"), c("unproven")], outcome: null, ratifiedBy: null })!.state).toBe("warn");
    expect(checkOf({ criteria: [c("unproven"), c("unmet")], outcome: null, ratifiedBy: null }))
      .toEqual({ state: "bad", summary: "0 of 2 done criteria met", title: "Checked against the spec" });
  });

  // 2026-11-19-1b72c: the per-criterion verdict was retired; a run without one has no check, no count, no "no verdict".
  it("RunSections_NoVerdict_ShowsNoCriteriaCount", () => {
    expect(checkOf(null)).toBeNull();
    expect(checkOf({ criteria: [], outcome: null, ratifiedBy: null })).toBeNull();
    const sections = runLogSections([], [], { acceptance: { criteria: [], outcome: null, ratifiedBy: null } } as unknown as RunSnapshot, null);
    expect(sections.map((s) => s.kind)).not.toContain("check");
    expect(sections.map((s) => s.meta).join(" ")).not.toMatch(/done criteria met|no verdict/);
  });

  it("checkOf_MastersReported_IsTheMastersReport_WithItsFollowUps_NotZeroOfZero", () => {
    const reported = { criteria: [], outcome: null, ratifiedBy: null, reports: [{ markdown: "## Done", specId: "s1" }] };
    expect(checkOf(reported)).toEqual({ state: "done", summary: "reported", title: "The master's report" });
    expect(checkOf({ ...reported, followUps: [{ goal: "g", why: "w" }, { goal: "h", why: "w" }] })!.summary).toBe("reported · 2 follow-ups");
  });

  it("checkOf_UnmeasuredAccount_IsRed_WithOrWithoutVerdicts", () => {
    const unmeasured = [{ repositories: "portal-worker", reason: "red build" }];
    expect(checkOf({ criteria: [], outcome: null, ratifiedBy: null, unmeasured }))
      .toEqual({ state: "bad", summary: "not measured", title: "Checked against the spec" });
    const c = { text: "x", status: "met" as const, reason: null };
    expect(checkOf({ criteria: [c], outcome: null, ratifiedBy: null, unmeasured })!.summary).toBe("1 of 1 done criteria met · not measured");
  });
});
