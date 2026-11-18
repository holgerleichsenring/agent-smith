import { describe, it, expect } from "vitest";
import { composeTimeBadge, isStoryRow, splitSpecPrefix } from "../runStepRail";
import type { RunStepRow } from "../runStepsApi";

// p0395: spliced spec steps carry their spec as structured data, never as a
// per-row label prefix. Current servers send RunStepView.specId with clean
// names; payloads from older servers still carry the composed "p<id>: <label>"
// names, so the rail splits the known prefix defensively.

function row(over: Partial<RunStepRow>): RunStepRow {
  return {
    stepIndex: 0, stepName: "Fetch ticket", displayName: null, commandName: null,
    status: "success", durationSeconds: 1, resultMessage: null,
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
    ...over,
  };
}

describe("splitSpecPrefix", () => {
  it("splits the known spec-qualified shape into spec and label", () => {
    expect(splitSpecPrefix("p19106a: Generate plan"))
      .toEqual({ specId: "p19106a", label: "Generate plan" });
  });

  it("leaves labels without the shape untouched", () => {
    expect(splitSpecPrefix("Fetch ticket")).toEqual({ specId: null, label: "Fetch ticket" });
    expect(splitSpecPrefix("prod: deploy")).toEqual({ specId: null, label: "prod: deploy" });
  });

  // p0507: a spec id is minted from a date and a four-hex random suffix. This split is
  // the dashboard's OWN copy of RunStepsReader.SpecQualifiedRegex — teaching only the
  // backend leaves the raw prefix rendered here, in the rail the operator actually reads.
  it("RunStepRail_DateMintedSpec_RendersWithoutTheRawPrefix", () => {
    expect(splitSpecPrefix("2026-08-24-8a3f: Generate plan"))
      .toEqual({ specId: "2026-08-24-8a3f", label: "Generate plan" });
  });

  // 2026-09-03-2f81: specs cut from one piece of work share a number and differ by a
  // trailing letter. Unwidened, the rail reads the id as ending at the hex and renders
  // the letter as the first character of the step label.
  it("RunStepRail_SeriesMember_CarriesItsLetterIntoTheSpecId", () => {
    expect(splitSpecPrefix("2026-08-24-8a3fb: Generate plan"))
      .toEqual({ specId: "2026-08-24-8a3fb", label: "Generate plan" });
  });

  it("still leaves a date that is not a minted id alone", () => {
    expect(splitSpecPrefix("2026-13-99-zzzz: nightly"))
      .toEqual({ specId: null, label: "2026-13-99-zzzz: nightly" });
  });
});


// p0404: the step's wall-clock split, composed for the detail pane's meta line.
// The server owns the numbers; the rail only names them.
describe("composeTimeBadge", () => {
  it("names model, sandbox and the scaffolding remainder", () => {
    const badge = composeTimeBadge(
      row({
        sandboxCommands: 2,
        time: { modelMs: 1500, throttleMs: 0, sandboxMs: 9050, scaffoldingMs: 9450 },
      }),
    );

    expect(badge).toBe("1.5s model · 9.1s sandbox (2 cmd) · 9.4s scaffolding");
  });

  it("names the throttle share only when there was one", () => {
    const badge = composeTimeBadge(
      row({ time: { modelMs: 2000, throttleMs: 400, sandboxMs: 0, scaffoldingMs: 100 } }),
    );

    expect(badge).toBe("2.0s model · 0.4s throttled · 0.1s scaffolding");
  });

  it("omits the remainder while the step is still running", () => {
    const badge = composeTimeBadge(
      row({
        status: "running",
        time: { modelMs: 700, throttleMs: 0, sandboxMs: 0, scaffoldingMs: null },
      }),
    );

    expect(badge).toBe("0.7s model");
  });

  it("shows nothing when the server attributed no time", () => {
    expect(composeTimeBadge(row({}))).toBeNull();
    expect(
      composeTimeBadge(row({ time: { modelMs: 0, throttleMs: 0, sandboxMs: 0, scaffoldingMs: 0 } })),
    ).toBeNull();
  });

});

// p0405: the server delivers ONE ordered sequence — executed steps followed by
// planned ones. The rail renders it as delivered: it does not compute the spec
// block, multiply it by a spec count, or decide what a missing field means.

// 2026-11-17-6d9ed: the run log folds a section's mechanics by this rule.
describe("isStoryRow", () => {
  it("isStoryRow_InternalFolds_GateOnlyWithAFinding_FailingAlwaysShows", () => {
    expect(isStoryRow({ status: "ok", stepClass: "internal" })).toBe(false);
    expect(isStoryRow({ status: "ok", stepClass: "gate" })).toBe(false);
    expect(isStoryRow({ status: "ok", stepClass: "gate", hasFinding: true })).toBe(true);
    expect(isStoryRow({ status: "fail", stepClass: "internal" })).toBe(true);
    expect(isStoryRow({ status: "ok", stepClass: null })).toBe(true);
  });
});
