import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CallSizePlot } from "../CallSizePlot";
import { niceMax, scaleSeries } from "../callSeries";
import type { RunCallPoint } from "@/lib/runStoryApi";

// p0423b: the shape that named the wall — the prompt grows, the answer shrinks, then
// nothing. It has to be visible as a SHAPE, and the two measures must never share one
// y-axis: on a single scale the answer collapse is a flat line on the floor.

function call(index: number, promptChars: number, answerChars: number, outcome = "Ok"): RunCallPoint {
  return {
    index,
    phaseId: "p1",
    stepIndex: 1,
    role: "agentic-executor",
    model: "sonnet",
    promptChars,
    answerChars,
    durationMs: 9_300,
    throttleWaitMs: 0,
    outcome,
    attempt: 1,
  };
}

const RUN_26: RunCallPoint[] = [
  call(6, 151_040, 3_886),
  call(7, 216_004, 3_886),
  call(8, 278_267, 2_750),
  call(9, 340_747, 969),
  call(10, 356_632, 0, "Cancelled"),
];

describe("CallSizePlot", () => {
  it("CallSizePlot_ShowsPromptAgainstAnswerPerCall", () => {
    render(<CallSizePlot calls={RUN_26} />);

    const plot = screen.getByTestId("call-size-plot");
    expect(plot).toHaveAttribute("data-calls", "5");
    // Two panels, one per measure — never two y-axes on one plot.
    expect(screen.getByTestId("call-size-prompt")).toHaveAttribute("data-points", "5");
    expect(screen.getByTestId("call-size-answer")).toHaveAttribute("data-points", "5");
  });

  it("CallSizePlot_MarksTheCallThatReturnedNothing", () => {
    render(<CallSizePlot calls={RUN_26} />);

    // A zero-length answer draws no visible area — it gets its own marker, or the most
    // diagnostic point in the series would be the one nobody can see.
    expect(screen.getAllByTestId("call-size-answer-marker")).toHaveLength(1);
    expect(screen.getByTestId("call-size-silent")).toHaveTextContent("One call returned nothing");
  });

  it("CallSizePlot_ReadoutOpensOnTheRunsLastCall", () => {
    render(<CallSizePlot calls={RUN_26} />);

    const readout = screen.getByTestId("call-size-readout");
    expect(readout).toHaveAttribute("data-call", "10");
    expect(readout).toHaveTextContent("prompt 357k");
    expect(readout).toHaveTextContent("answer 0");
    expect(readout).toHaveTextContent("cancelled");
  });

  it("CallSizePlot_WithNoCalls_SaysSo_RatherThanDrawingNothing", () => {
    render(<CallSizePlot calls={[]} />);

    expect(screen.getByTestId("call-size-plot-empty")).toBeInTheDocument();
  });

  it("CallSizeGeometry_MeasuresFromTheBaseline_SoACollapseFalls", () => {
    const max = niceMax(3_886);
    const points = scaleSeries([3_886, 969, 0], 720, 132, max);

    expect(points[0].y).toBeLessThan(points[1].y);
    expect(points[2].y).toBe(132);
    expect(points[0].x).toBe(0);
    expect(points[2].x).toBe(720);
  });
});
