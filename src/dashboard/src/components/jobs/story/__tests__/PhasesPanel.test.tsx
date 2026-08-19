import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { PhasesPanel } from "../PhasesPanel";
import type { RunPhaseRow } from "@/lib/runPhasesApi";

// p0466: a finished phase is a place you can go back to. The panel lists the
// phases the run was cut into and opens one to show what it decided and the spec
// it executed — read from the server's phase rows, never parsed out of a label.

const fetchMock = vi.fn();

function phase(over: Partial<RunPhaseRow> = {}): RunPhaseRow {
  return {
    phaseId: "p19213a",
    ordinal: 1,
    title: "Make the thing exist",
    status: "done",
    startedAt: "2026-08-19T09:00:00Z",
    endedAt: "2026-08-19T09:05:00Z",
    verdict: null,
    decisions: [
      {
        stepIndex: 4,
        name: "sqlite",
        reason: "smallest footprint",
        category: "persistence",
        recordedAt: "2026-08-19T09:02:00Z",
      },
    ],
    steps: [],
    ...over,
  };
}

function respond(phases: RunPhaseRow[], record: string | null = "phase: p19213a\n") {
  fetchMock.mockImplementation((url: string) =>
    Promise.resolve(
      url.includes("/phases/")
        ? { ok: true, status: 200, json: async () => ({ phase: phases[0], record }) }
        : { ok: true, status: 200, json: async () => ({ phases }) },
    ),
  );
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

describe("PhasesPanel", () => {
  it("PhasesPanel_FinishedPhase_OpensItsDecisionsAndExecutedSpec", async () => {
    respond([phase()]);
    render(<PhasesPanel runId="r1" revision={0} />);

    const toggle = await screen.findByTestId("phase-toggle-p19213a");
    expect(screen.queryByTestId("phase-body-p19213a")).toBeNull();

    fireEvent.click(toggle);

    const body = await screen.findByTestId("phase-body-p19213a");
    expect(body.textContent).toContain("sqlite — smallest footprint");
    await waitFor(() =>
      expect(screen.getByTestId("phase-record-p19213a").textContent).toContain("phase: p19213a"),
    );
  });

  it("PhasesPanel_PhaseWithoutARecord_NamesWhatWasLookedUp", async () => {
    respond([phase()], null);
    render(<PhasesPanel runId="r1" revision={0} />);

    fireEvent.click(await screen.findByTestId("phase-toggle-p19213a"));

    await waitFor(() =>
      expect(screen.getByTestId("phase-record-empty-p19213a").textContent).toContain("p19213a"),
    );
  });

  it("PhasesPanel_RunWithoutPhases_RendersNothing", async () => {
    respond([]);
    render(<PhasesPanel runId="r1" revision={0} />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId("phases-panel")).toBeNull();
  });

  it("PhasesPanel_FailedPhase_ShowsTheVerdictItStoppedOn", async () => {
    respond([phase({ status: "failed", verdict: "dotnet test exited 1", decisions: [] })]);
    render(<PhasesPanel runId="r1" revision={0} />);

    const meta = await screen.findByTestId("phase-meta-p19213a");
    expect(meta.textContent).toContain("dotnet test exited 1");
  });
});
