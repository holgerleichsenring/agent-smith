import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { SpecsPanel } from "../SpecsPanel";
import type { RunSpecRow } from "@/lib/runSpecsApi";

// p0466: a finished spec is a place you can go back to. The panel lists the
// specs the run executed and opens one to show what it decided and the spec body
// it executed — read from the server's spec rows, never parsed out of a label.

const fetchMock = vi.fn();

function spec(over: Partial<RunSpecRow> = {}): RunSpecRow {
  return {
    specId: "p19213a",
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

function respond(specs: RunSpecRow[], record: string | null = "spec: p19213a\n") {
  fetchMock.mockImplementation((url: string) =>
    Promise.resolve(
      url.includes("/specs/")
        ? { ok: true, status: 200, json: async () => ({ spec: specs[0], record }) }
        : { ok: true, status: 200, json: async () => ({ specs }) },
    ),
  );
}

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

describe("SpecsPanel", () => {
  it("SpecsPanel_RendersRows", async () => {
    respond([spec(), spec({ specId: "p19213b", ordinal: 2, title: "Make it readable" })]);
    render(<SpecsPanel runId="r1" revision={0} />);

    const panel = await screen.findByTestId("specs-panel");
    expect(panel.textContent).toContain("Specs");
    expect(screen.getByTestId("spec-p19213a").textContent).toContain("Make the thing exist");
    expect(screen.getByTestId("spec-p19213b").textContent).toContain("Make it readable");
    expect(fetchMock.mock.calls.map((c) => String(c[0]))).toContainEqual(
      expect.stringContaining("/api/runs/r1/specs"),
    );
  });

  it("SpecsPanel_FinishedSpec_OpensItsDecisionsAndExecutedSpec", async () => {
    respond([spec()]);
    render(<SpecsPanel runId="r1" revision={0} />);

    const toggle = await screen.findByTestId("spec-toggle-p19213a");
    expect(screen.queryByTestId("spec-body-p19213a")).toBeNull();

    fireEvent.click(toggle);

    const body = await screen.findByTestId("spec-body-p19213a");
    expect(body.textContent).toContain("sqlite — smallest footprint");
    await waitFor(() =>
      expect(screen.getByTestId("spec-outcome-p19213a").textContent).toContain("spec: p19213a"),
    );
  });

  it("SpecsPanel_SpecWithoutARecord_NamesWhatWasLookedUp", async () => {
    respond([spec()], null);
    render(<SpecsPanel runId="r1" revision={0} />);

    fireEvent.click(await screen.findByTestId("spec-toggle-p19213a"));

    await waitFor(() =>
      expect(screen.getByTestId("spec-outcome-empty-p19213a").textContent).toContain("p19213a"),
    );
  });

  it("SpecsPanel_RunWithoutSpecs_RendersNothing", async () => {
    respond([]);
    render(<SpecsPanel runId="r1" revision={0} />);

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(screen.queryByTestId("specs-panel")).toBeNull();
  });

  it("SpecsPanel_FailedSpec_ShowsTheVerdictItStoppedOn", async () => {
    respond([spec({ status: "failed", verdict: "dotnet test exited 1", decisions: [] })]);
    render(<SpecsPanel runId="r1" revision={0} />);

    const meta = await screen.findByTestId("spec-meta-p19213a");
    expect(meta.textContent).toContain("dotnet test exited 1");
  });

  // 2026-09-17-0e79c: a spec handed back on a false premise was never built. The badge map
  // falls back to "not started" for a status it does not know, which of a spec the run
  // stopped on would be plainly false.
  it("SpecsPanel_HandedBackSpec_DoesNotReadAsNotStartedOrAsFailed", async () => {
    respond([
      spec({
        status: "handed_back",
        verdict: 'False premise in p19213a: "the bus client is a singleton"',
        decisions: [],
      }),
    ]);
    render(<SpecsPanel runId="r1" revision={0} />);

    const row = await screen.findByTestId("spec-p19213a");
    expect(row.textContent).toContain("handed back");
    expect(row.textContent).not.toContain("not started");
    expect(row.textContent).not.toContain("failed");
  });
});
