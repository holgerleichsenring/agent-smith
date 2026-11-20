import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

// 2026-11-19-1b72d: an earlier spec's steps — read from its record, their states from the run's passes; passes the page
// already holds are not read again, and a read that fails says so.

const fetchRunSpec = vi.fn();
const fetchRunPasses = vi.fn();
vi.mock("@/lib/runSpecsApi", () => ({ fetchRunSpec: (...a: unknown[]) => fetchRunSpec(...a) }));
vi.mock("@/lib/ticketsApi", () => ({ fetchRunPasses: (...a: unknown[]) => fetchRunPasses(...a) }));

const { StepSpecSteps } = await import("../StepSpecSteps");

const RECORD = "spec: s-1\ngoal: g\nsteps:\n  - id: a\n    action: First\n  - id: b\n    action: Second\n";
const PASSES = {
  runId: "r1", specs: [{ specId: "s-1", passes: [{ ordinal: 1, stepsMoved: [{ entryId: "a", activity: "First", from: "pending", to: "done", cause: "model_update", pass: 1 }] }] }],
};

beforeEach(() => {
  fetchRunSpec.mockReset();
  fetchRunPasses.mockReset();
});

describe("StepSpecSteps", () => {
  it("StepSpecSteps_GivenPasses_AreNotReadAgain", async () => {
    fetchRunSpec.mockResolvedValue({ spec: {}, record: RECORD });
    render(<StepSpecSteps runId="r1" specId="s-1" ledger={null} ledgerIsThisSpec={false}
      passes={{ data: PASSES as never, loading: false, failed: false }} />);

    await waitFor(() => expect(screen.getAllByTestId("step-spec-step")).toHaveLength(2));
    expect(screen.getAllByTestId("step-spec-step").map((r) => r.getAttribute("data-state"))).toEqual(["done", "open"]);
    expect(fetchRunPasses).not.toHaveBeenCalled();
    // 2026-11-20-44b6: the row must not carry the spec record's own row class, whose two-column grid squeezed its text.
    for (const row of screen.getAllByTestId("step-spec-step")) expect(row.classList.contains("rl-spec-step")).toBe(false);
  });

  it("StepSpecSteps_PassesFailed_ShowsTheStepsAndSaysTheirStatesCouldNotBeRead", async () => {
    fetchRunSpec.mockResolvedValue({ spec: {}, record: RECORD });
    fetchRunPasses.mockRejectedValue(new Error("500"));
    render(<StepSpecSteps runId="r1" specId="s-1" ledger={null} ledgerIsThisSpec={false} />);

    await waitFor(() => expect(screen.getByTestId("step-spec-states-failed")).toHaveTextContent("the steps’ states could not be read"));
    expect(screen.getAllByTestId("step-spec-step").map((r) => [r.getAttribute("data-state"), r.textContent])).toEqual([
      ["unknown", "1First"], ["unknown", "2Second"],
    ]);
    expect(screen.getByTestId("step-part-heading")).toHaveTextContent(/^Steps of the spec$/);
  });

  it("StepSpecSteps_GivenPassesFailed_AlsoSaysTheStatesCouldNotBeRead", async () => {
    fetchRunSpec.mockResolvedValue({ spec: {}, record: RECORD });
    render(<StepSpecSteps runId="r1" specId="s-1" ledger={null} ledgerIsThisSpec={false} passes={{ data: null, loading: false, failed: true }} />);

    await waitFor(() => expect(screen.getByTestId("step-spec-states-failed")).toBeInTheDocument());
    expect(screen.getAllByTestId("step-spec-step")).toHaveLength(2);
  });

  it("StepSpecSteps_AFailedRead_SaysItCouldNotBeRead", async () => {
    fetchRunSpec.mockRejectedValue(new Error("500"));
    fetchRunPasses.mockResolvedValue(PASSES);
    render(<StepSpecSteps runId="r1" specId="s-1" ledger={null} ledgerIsThisSpec={false} />);

    await waitFor(() => expect(screen.getByTestId("step-spec-steps-empty")).toHaveTextContent("the spec's steps could not be read"));
  });
});
