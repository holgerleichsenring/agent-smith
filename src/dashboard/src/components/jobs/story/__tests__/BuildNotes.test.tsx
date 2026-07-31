import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { BuildNotes } from "../BuildNotes";
import type { RunDecisionRow } from "@/lib/runStepsApi";

// p0388c: the decision notes read their rows from the durable RunDecision
// projection, which now carries the producer's category again. A row written
// before the column existed carries none — it renders without the segment
// rather than with a placeholder.

const fetchMock = vi.fn();

function decision(over: Partial<RunDecisionRow> = {}): RunDecisionRow {
  return {
    stepIndex: 4,
    name: "sqlite",
    reason: "smallest footprint",
    category: "persistence",
    recordedAt: "2026-07-30T09:05:00Z",
    ...over,
  };
}

function renderNotes(decisions: RunDecisionRow[]) {
  fetchMock.mockResolvedValue({ ok: true, json: async () => ({ decisions }) });
  return render(<BuildNotes runId="r1" events={[]} />);
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

describe("BuildNotes", () => {
  it("BuildNotes_DecisionWithCategory_RendersCategorySegment", async () => {
    renderNotes([decision()]);

    const notes = await screen.findByTestId("build-notes");
    await waitFor(() => expect(notes).toHaveTextContent("sqlite — smallest footprint"));
    expect(notes.textContent).toContain("decision · persistence · ");
  });

  it("BuildNotes_DecisionWithoutCategory_RendersNoCategorySegment", async () => {
    renderNotes([decision({ category: null })]);

    const notes = await screen.findByTestId("build-notes");
    await waitFor(() => expect(notes).toHaveTextContent("sqlite"));
    // No fabricated qualifier: the meta line goes straight from the kind to the time.
    expect(notes.textContent).not.toContain("decision · persistence");
    expect(notes.textContent).toMatch(/decision · \d/);
  });

  it("BuildNotes_NoDecisionsAndNoWrites_RendersNothing", async () => {
    renderNotes([]);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId("build-notes")).toBeNull();
  });
});
