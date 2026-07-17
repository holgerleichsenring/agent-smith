import { describe, it, expect } from "vitest";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import type { NodeStatus } from "@/components/execution/TimingGutter";
import { EventType, type RunEvent } from "@/types/hub-events";
import {
  classifyBeat,
  deriveBeatStatus,
  mapStepsToBeats,
  buildVerifyView,
  parseExpectationJson,
  findRatifiedEvent,
  BEAT_ORDER,
} from "../beatMapping";

function step(label: string, status: NodeStatus = "ok"): ExecutionNodeProps {
  return {
    id: `step-${label.replace(/\s+/g, "-").toLowerCase()}`,
    label,
    status,
    depth: 0,
    startSeconds: 0,
    durationSeconds: 1,
    totalSeconds: 10,
    durationLabel: "1s",
  };
}

function ratifiedEvent(
  outcome: string,
  ratifiedJson: string,
  editDistance = 0,
): RunEvent {
  return {
    runId: "run-1",
    type: EventType.ExpectationRatified,
    timestamp: "2026-07-16T10:00:00Z",
    draftJson: "{}",
    ratifiedJson,
    outcome,
    ratifiedBy: "holger",
    editDistance,
  } as RunEvent;
}

const DRAFT_JSON = JSON.stringify({
  Observed: "Login button does nothing",
  Expected: ["Clicking login authenticates the user", "A failed login shows an error"],
  Constraints: ["No new dependencies"],
  OpenQuestion: null,
});

describe("classifyBeat", () => {
  it("ClassifyBeat_CanonicalLabels_MapToTheirBeat", () => {
    expect(classifyBeat("Fetch ticket")).toBe("ticket");
    expect(classifyBeat("Analyze codebase")).toBe("plan");
    expect(classifyBeat("Generate plan")).toBe("plan");
    expect(classifyBeat("Execute plan")).toBe("building");
    expect(classifyBeat("Run verify phase")).toBe("verify");
    expect(classifyBeat("Create pull request")).toBe("outcome");
  });

  it("ClassifyBeat_FrameworkSetupSteps_MatchNothing", () => {
    // These fold into the current beat by carry-forward, not top-level.
    expect(classifyBeat("Load catalog")).toBeNull();
    expect(classifyBeat("Publish pipeline name")).toBeNull();
    expect(classifyBeat("Prepare environment")).toBeNull();
  });
});

describe("deriveBeatStatus", () => {
  it("DeriveBeatStatus_NoSteps_IsIdle", () => {
    expect(deriveBeatStatus([])).toBe("idle");
  });
  it("DeriveBeatStatus_AnyFail_IsFail", () => {
    expect(deriveBeatStatus(["ok", "fail", "run"])).toBe("fail");
  });
  it("DeriveBeatStatus_AllOk_IsDone", () => {
    expect(deriveBeatStatus(["ok", "ok"])).toBe("done");
  });
  it("DeriveBeatStatus_AnyRunning_IsActive", () => {
    expect(deriveBeatStatus(["ok", "run"])).toBe("active");
  });
  it("DeriveBeatStatus_ParkedForInput_IsActive", () => {
    expect(deriveBeatStatus(["input"])).toBe("active");
  });
  it("DeriveBeatStatus_AllWaiting_IsIdle", () => {
    expect(deriveBeatStatus(["wait", "wait"])).toBe("idle");
  });
});

describe("mapStepsToBeats", () => {
  it("StoryBar_FrameworkSteps_FoldedIntoBeats_NotTopLevel", () => {
    const nodes = [
      step("Load catalog"),
      step("Publish pipeline name"),
      step("Fetch ticket"),
      step("Analyze codebase"),
      step("Execute plan", "run"),
      step("Create pull request", "wait"),
    ];
    const beats = mapStepsToBeats(nodes);
    // Always exactly the five story beats — framework steps never appear as beats.
    expect(beats.map((b) => b.key)).toEqual(BEAT_ORDER);
    // "Load catalog" / "Publish pipeline name" folded into the leading (ticket) beat.
    const ticket = beats.find((b) => b.key === "ticket")!;
    expect(ticket.stepIds).toContain("step-load-catalog");
    expect(ticket.stepIds).toContain("step-publish-pipeline-name");
    expect(ticket.stepIds).toContain("step-fetch-ticket");
    // No beat is labelled after a framework step.
    expect(beats.every((b) => b.label !== "Load catalog")).toBe(true);
  });

  it("MapStepsToBeats_FixBugStory_DerivesPerBeatStatus", () => {
    const nodes = [
      step("Fetch ticket", "ok"),
      step("Analyze codebase", "ok"),
      step("Generate plan", "ok"),
      step("Execute plan", "run"),
      step("Run verify phase", "wait"),
      step("Create pull request", "wait"),
    ];
    const beats = mapStepsToBeats(nodes);
    const byKey = Object.fromEntries(beats.map((b) => [b.key, b]));
    expect(byKey.ticket.status).toBe("done");
    expect(byKey.plan.status).toBe("done");
    expect(byKey.building.status).toBe("active");
    expect(byKey.verify.status).toBe("idle");
    expect(byKey.outcome.status).toBe("idle");
    // Anchor is the beat's first folded step.
    expect(byKey.building.anchorId).toBe("step-execute-plan");
  });

  it("MapStepsToBeats_EmptyBeat_HasNoAnchor", () => {
    const beats = mapStepsToBeats([step("Fetch ticket")]);
    const outcome = beats.find((b) => b.key === "outcome")!;
    expect(outcome.stepIds).toEqual([]);
    expect(outcome.anchorId).toBeNull();
    expect(outcome.status).toBe("idle");
  });

  it("MapStepsToBeats_Monotonic_LateEarlyMatchDoesNotGoBackwards", () => {
    // A late step whose label matches an earlier keyword must not drag the
    // story back — it stays in the beat already reached.
    const nodes = [
      step("Fetch ticket"),
      step("Execute plan", "ok"),
      step("Analyze PR diff", "ok"), // "analy" would match plan; must stay >= building
    ];
    const beats = mapStepsToBeats(nodes);
    const plan = beats.find((b) => b.key === "plan")!;
    expect(plan.stepIds).not.toContain("step-analyze-pr-diff");
  });
});

describe("buildVerifyView", () => {
  it("Verify_Verbatim_RendersGreen", () => {
    const view = buildVerifyView([ratifiedEvent("verbatim", DRAFT_JSON)]);
    expect(view.tone).toBe("green");
    expect(view.ratified).toBe(true);
    expect(view.expectation?.expected).toHaveLength(2);
  });

  it("Verify_Edited_RendersGreen_WithEditDistance", () => {
    const view = buildVerifyView([ratifiedEvent("edited", DRAFT_JSON, 4)]);
    expect(view.tone).toBe("green");
    expect(view.editDistance).toBe(4);
  });

  it("Verify_Rejected_RendersRose", () => {
    const view = buildVerifyView([ratifiedEvent("rejected", DRAFT_JSON)]);
    expect(view.tone).toBe("rose");
    expect(view.ratified).toBe(false);
  });

  it("Verify_Unratified_NeverRendersGreen", () => {
    const view = buildVerifyView([ratifiedEvent("unratified", DRAFT_JSON)]);
    expect(view.tone).toBe("neutral");
    expect(view.ratified).toBe(false);
  });

  it("Verify_NoRatificationEvent_NeverRendersGreen", () => {
    const view = buildVerifyView([]);
    expect(view.outcome).toBe("none");
    expect(view.tone).toBe("neutral");
    expect(view.expectation).toBeNull();
  });

  it("FindRatifiedEvent_TakesTheLatest", () => {
    const events = [ratifiedEvent("rejected", DRAFT_JSON), ratifiedEvent("verbatim", DRAFT_JSON)];
    expect(findRatifiedEvent(events)?.outcome).toBe("verbatim");
  });
});

describe("parseExpectationJson", () => {
  it("ParseExpectationJson_PascalCase_ReadsCriteria", () => {
    const parsed = parseExpectationJson(DRAFT_JSON);
    expect(parsed?.observed).toContain("Login button");
    expect(parsed?.expected).toHaveLength(2);
    expect(parsed?.constraints).toEqual(["No new dependencies"]);
  });

  it("ParseExpectationJson_CamelCase_AlsoReadsCriteria", () => {
    const parsed = parseExpectationJson(
      JSON.stringify({ observed: "x", expected: ["a"], constraints: [] }),
    );
    expect(parsed?.expected).toEqual(["a"]);
  });

  it("ParseExpectationJson_Garbage_ReturnsNull", () => {
    expect(parseExpectationJson("not json")).toBeNull();
  });
});
