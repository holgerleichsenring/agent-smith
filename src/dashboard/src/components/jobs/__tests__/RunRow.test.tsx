import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RunRow } from "../RunRow";
import type { RunSnapshot } from "@/types/hub-events";

// p0343c (pixel identity): RunRow emits the runs-list mock's .rrow DOM — the
// st-* status class drives the dot, .tick/.ttl carry the real ticket ref and
// title, the .spine renders ONLY from server-computed beats, queued rows show
// their FIFO place + reason, and the delete button stays always visible.

const base: RunSnapshot = {
  runId: "2026-06-03T10-00-00-abcd",
  pipeline: "fix-bug",
  trigger: "ticket",
  repos: ["server", "web"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: new Date(Date.now() - 64_000).toISOString(),
  finishedAt: null,
  sandboxes: 2,
  stepIndex: 7,
  stepName: "AnalyzeCode",
  totalSteps: 16,
  lastEventType: "StepStarted",
  costUsd: 0,
  llmCalls: 0,
  ticketId: "18803",
  ticketTitle: "AuthController coverage",
  agentName: "azure_openai",
  cancelRequested: false,
};

describe("RunRow", () => {
  it("RunRow_StatusMapsToMockStClass", () => {
    const cases: Array<[string, string]> = [
      ["running", "st-run"],
      ["queued", "st-q"],
      ["success", "st-ok"],
      ["failed", "st-bad"],
      ["cancelled", "st-q"],
      ["waiting_for_input", "st-need"],
    ];
    for (const [status, cls] of cases) {
      const { unmount } = render(<RunRow snapshot={{ ...base, status }} />);
      expect(screen.getByTestId(`run-row-${base.runId}`).className).toContain(cls);
      unmount();
    }
  });

  it("RunRow_TicketRefAndTitle_RenderInTickAndTtl", () => {
    render(<RunRow snapshot={base} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.querySelector(".tick")).toHaveTextContent("#18803");
    expect(row.querySelector(".ttl")).toHaveTextContent("AuthController coverage");
  });

  it("RunRow_RunningStatus_ShowsCurrentStepInActivityLine", () => {
    render(<RunRow snapshot={base} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.querySelector(".act")).toHaveTextContent("now: AnalyzeCode");
    expect(screen.getByTestId(`run-row-${base.runId}-progress`)).toHaveTextContent("7/16");
  });

  it("RunRow_Spine_RendersOnlyFromServerBeats", () => {
    const { unmount } = render(<RunRow snapshot={base} />);
    expect(screen.queryByTestId("run-row-spine")).not.toBeInTheDocument();
    unmount();
    render(
      <RunRow
        snapshot={{
          ...base,
          beats: { ticket: "done", plan: "done", building: "active", verify: "pending", outcome: "pending" },
        }}
      />,
    );
    const spine = screen.getByTestId("run-row-spine");
    const dots = spine.querySelectorAll("i");
    expect(dots).toHaveLength(5);
    expect(dots[0].className).toBe("d");
    expect(dots[2].className).toBe("n");
    expect(dots[3].className).toBe("");
  });

  it("RunRow_RowLinksToRunDetail", () => {
    render(<RunRow snapshot={base} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.tagName).toBe("A");
    expect(row).toHaveAttribute("href", `/jobs/${encodeURIComponent(base.runId)}`);
  });

  it("RunRow_QueuedStatus_ShowsPositionAndReason", () => {
    // p0320d: a queued run shows its FIFO place and WHY it waits — never the
    // misleading stepIndex/totalSteps fill that reads like a stalled run.
    render(
      <RunRow
        snapshot={{
          ...base,
          status: "queued",
          summary: "waiting for sandbox capacity",
          queuePosition: 3,
        }}
      />,
    );
    expect(screen.getByTestId(`run-row-${base.runId}-progress`)).toHaveTextContent("pos 3");
    expect(screen.getByText("waiting for sandbox capacity")).toBeInTheDocument();
    expect(screen.queryByText("7/16")).not.toBeInTheDocument();
  });

  it("RunRow_QueuedWithoutPosition_ShowsPlainQueuedLabel", () => {
    render(<RunRow snapshot={{ ...base, status: "queued", queuePosition: null }} />);
    expect(screen.getByTestId(`run-row-${base.runId}-progress`)).toHaveTextContent("queued");
  });

  it("RunRow_FinishedWithoutBeats_ShowsOutcomePill", () => {
    const { unmount } = render(
      <RunRow snapshot={{ ...base, status: "success", finishedAt: new Date().toISOString() }} />,
    );
    let row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.querySelector(".pill.ok")).toHaveTextContent("done");
    unmount();
    render(
      <RunRow snapshot={{ ...base, status: "failed", finishedAt: new Date().toISOString() }} />,
    );
    row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.querySelector(".pill.bad")).toHaveTextContent("failed");
  });

  it("RunRow_CancelRequested_BadgeVisible_AnyStatus", () => {
    // p0330: the durable cancelRequested flag shows on the live list row.
    const { unmount } = render(
      <RunRow snapshot={{ ...base, status: "queued", cancelRequested: true }} />,
    );
    expect(screen.getByTestId("cancel-requested-badge")).toHaveTextContent("cancelling…");
    unmount();
    render(
      <RunRow
        snapshot={{
          ...base,
          status: "success",
          finishedAt: new Date().toISOString(),
          cancelRequested: true,
        }}
      />,
    );
    expect(screen.getByTestId("cancel-requested-hint")).toHaveTextContent("cancel was requested");
  });

  it("RunRow_DeleteButton_VisibleWithoutHover", () => {
    // p0345b: the per-row delete is always visible — no opacity-0 hover reveal.
    render(<RunRow snapshot={base} />);
    expect(screen.getByTestId(`delete-run-${base.runId}`)).toBeInTheDocument();
    expect(screen.getByTestId(`run-row-${base.runId}`).innerHTML).not.toContain("opacity-0");
  });

  it("RunRow_NoTicket_FallsBackToRunIdAndPipeline_NeverSynthesises", () => {
    render(<RunRow snapshot={{ ...base, repos: [], ticketId: null, ticketTitle: null }} />);
    const row = screen.getByTestId(`run-row-${base.runId}`);
    expect(row.querySelector(".tick")).toHaveTextContent(`#${base.runId.slice(0, 8)}`);
    expect(row.querySelector(".ttl")).toHaveTextContent("fix-bug");
    expect(screen.queryByText("AuthController coverage")).not.toBeInTheDocument();
  });
});
