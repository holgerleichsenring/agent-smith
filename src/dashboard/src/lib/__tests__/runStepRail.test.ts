import { describe, it, expect } from "vitest";
import { composeTimeBadge, splitSpecPrefix, toRailNodes } from "../runStepRail";
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

describe("toRailNodes spec handling", () => {
  it("prefers the server's structured spec id and shows the clean label", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 0, displayName: "Generate plan", specId: "p19106a" }),
    ]);

    expect(nodes[0].label).toBe("Generate plan");
    expect(nodes[0].specId).toBe("p19106a");
  });

  it("splits a legacy prefixed label from an old server defensively", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 0, displayName: "p19106a: Generate plan" }),
    ]);

    expect(nodes[0].label).toBe("Generate plan");
    expect(nodes[0].specId).toBe("p19106a");
  });

  it("carries no spec on an unspliced step", () => {
    const nodes = toRailNodes([row({ stepIndex: 0, displayName: "Fetch ticket" })]);

    expect(nodes[0].label).toBe("Fetch ticket");
    expect(nodes[0].specId).toBeNull();
  });

  // p0398: the server's classification rides along; an old server without it
  // yields milestone-like rows (class null, no finding) — nothing hides.
  it("carries the server's step class and finding flag through to the node", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 0, stepClass: "gate", hasFinding: true }),
      row({ stepIndex: 1 }),
    ]);

    expect(nodes[0].stepClass).toBe("gate");
    expect(nodes[0].hasFinding).toBe(true);
    expect(nodes[1].stepClass).toBeNull();
    expect(nodes[1].hasFinding).toBe(false);
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

  it("rides through toRailNodes onto the node", () => {
    const nodes = toRailNodes([
      row({ time: { modelMs: 1000, throttleMs: 0, sandboxMs: 0, scaffoldingMs: 500 } }),
    ]);

    expect(nodes[0].timeBadge).toBe("1.0s model · 0.5s scaffolding");
  });
});

// p0405: the server delivers ONE ordered sequence — executed steps followed by
// planned ones. The rail renders it as delivered: it does not compute the spec
// block, multiply it by a spec count, or decide what a missing field means.
describe("toRailNodes planned steps", () => {
  const plannedRow = (over: Partial<RunStepRow>): RunStepRow =>
    row({
      planned: true, status: null, durationSeconds: null, resultMessage: null,
      llmCalls: null, costUsd: null, sandboxCommands: null, subAgents: null, ...over,
    });

  it("carries the planned marker through and reads the absent status as not-yet-run", () => {
    const nodes = toRailNodes([
      row({ stepIndex: 1, status: "success" }),
      plannedRow({ stepIndex: 2, displayName: "Execute the spec", specId: "p19106a" }),
    ]);

    expect(nodes[0].planned).toBe(false);
    expect(nodes[1].planned).toBe(true);
    expect(nodes[1].status).toBe("wait");
    expect(nodes[1].label).toBe("Execute the spec");
    expect(nodes[1].specId).toBe("p19106a");
  });

  it("shows a planned step no cost, no duration and no time split", () => {
    const nodes = toRailNodes([plannedRow({ stepIndex: 2 })]);

    expect(nodes[0].costBadge).toBeNull();
    expect(nodes[0].timeBadge).toBeNull();
    expect(nodes[0].durationLabel).toBe("");
    expect(nodes[0].durationSeconds).toBe(0);
  });
});
