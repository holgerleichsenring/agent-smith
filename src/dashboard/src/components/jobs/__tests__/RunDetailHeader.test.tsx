import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { RunDetailHeader } from "../RunDetailHeader";

// p0343c (pixel identity): the header is the run-viewer mock's — the ticket
// title as h1, the .spill status line with the real cancel/delete actions, and
// the .ident joined field strip (Run/Ticket/Pipeline/Agent/Repositories), each
// field present ONLY when the snapshot carries it.

const base = {
  pipeline: "fix-bug",
  ticketId: "123",
  ticketTitle: "Login button broken",
  runId: "run-abc",
  phrase: null,
  agentName: null,
  repoNames: [],
  connectionState: HubConnectionState.Connected,
  status: "success",
  cancelRequested: false,
  costUsd: null,
  reservedGiMinutes: null,
};

describe("RunDetailHeader", () => {
  it("RunDetail_Heading_ShowsTicketTitle_PipelineMovesToIdent", () => {
    render(<RunDetailHeader {...base} />);
    expect(screen.getByTestId("run-heading")).toHaveTextContent("Login button broken");
    // The pipeline lives in the .ident strip, not the headline.
    const ident = screen.getByTestId("run-ident");
    expect(ident).toHaveTextContent("fix-bug");
  });

  it("RunDetail_NoTicketTitle_FallsBackToPipelineThenRun", () => {
    const { rerender } = render(<RunDetailHeader {...base} ticketTitle={null} />);
    expect(screen.getByTestId("run-heading")).toHaveTextContent("fix-bug");
    rerender(<RunDetailHeader {...base} ticketTitle={null} pipeline={null} />);
    expect(screen.getByTestId("run-heading")).toHaveTextContent("run");
  });

  it("RunDetail_IdentStrip_RendersOnlyFieldsTheSnapshotCarries", () => {
    render(
      <RunDetailHeader
        {...base}
        agentName="azure_openai"
        repoNames={["server", "web", "worker"]}
      />,
    );
    expect(screen.getByTestId("run-ticket-id")).toHaveTextContent("123");
    expect(screen.getByTestId("run-agent-name")).toHaveTextContent("azure_openai");
    expect(screen.getByTestId("run-repos")).toHaveTextContent("3 · server");
  });

  it("RunDetail_IdentStrip_OmitsAbsentFields", () => {
    render(<RunDetailHeader {...base} ticketId={null} agentName={null} repoNames={[]} />);
    expect(screen.queryByTestId("run-ticket-id")).not.toBeInTheDocument();
    expect(screen.queryByTestId("run-agent-name")).not.toBeInTheDocument();
    expect(screen.queryByTestId("run-repos")).not.toBeInTheDocument();
  });

  it("RunDetail_Spill_MapsStatusToStateWord", () => {
    const { rerender } = render(<RunDetailHeader {...base} status="running" />);
    expect(screen.getByTestId("run-status-spill")).toHaveTextContent("Running");
    rerender(<RunDetailHeader {...base} status="waiting_for_input" />);
    expect(screen.getByTestId("run-status-spill")).toHaveTextContent("Needs you");
    rerender(<RunDetailHeader {...base} status="success" />);
    expect(screen.getByTestId("run-status-spill")).toHaveTextContent("Done");
    rerender(<RunDetailHeader {...base} status="failed" />);
    expect(screen.getByTestId("run-status-spill")).toHaveTextContent("Failed");
  });

  it("RunDetail_CancelButton_ShownForRunningQueuedWaiting_NotTerminal", () => {
    // p0330: cancellable states are running, queued AND waiting_for_input;
    // terminal statuses drop the button.
    const { rerender } = render(<RunDetailHeader {...base} status="success" />);
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
    rerender(<RunDetailHeader {...base} status="running" />);
    expect(screen.getByTestId("cancel-run-run-abc")).toBeInTheDocument();
    rerender(<RunDetailHeader {...base} status="queued" />);
    expect(screen.getByTestId("cancel-run-run-abc")).toBeInTheDocument();
    rerender(<RunDetailHeader {...base} status="cancelled" />);
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
  });

  it("RunDetail_CancelRequested_StaysVisibleAfterButtonGone", () => {
    // p0330: the durable flag outlives the button.
    render(<RunDetailHeader {...base} status="failed" cancelRequested={true} />);
    expect(screen.getByTestId("cancel-requested-hint")).toHaveTextContent("cancel was requested");
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
  });

  it("RunDetail_Cancelled_ShowsNoCancelRequestedIndicator", () => {
    render(<RunDetailHeader {...base} status="cancelled" cancelRequested={true} />);
    expect(screen.queryByTestId("cancel-requested-badge")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cancel-requested-hint")).not.toBeInTheDocument();
  });

  it("RunDetail_DeleteStaysVisible_AnyStatus", () => {
    render(<RunDetailHeader {...base} status="success" />);
    expect(screen.getByTestId("delete-run-run-abc")).toBeInTheDocument();
  });

  it("RunDetail_ReservedCapacity_LabeledAsReserved_NeverMoney", () => {
    // p0332: the figure is a RESERVATION (memory request × pod lifetime) — the
    // label must say "reserved" and never dress it up as money.
    render(<RunDetailHeader {...base} costUsd={0.07} reservedGiMinutes={6.2} />);
    const line = screen.getByTestId("run-cost-line");
    expect(line).toHaveTextContent("$0.07 LLM");
    const reserved = screen.getByTestId("run-reserved-capacity");
    expect(reserved).toHaveTextContent("reserved 6.2 Gi·min");
    expect(reserved.textContent).not.toContain("$");
  });

  it("RunDetail_NoReservedFigure_OmitsCostLine", () => {
    // Null = not computable (running run, pre-p0332 row) — show nothing rather
    // than a fake zero. The LLM cost itself lives on the side rail.
    render(<RunDetailHeader {...base} costUsd={0.07} reservedGiMinutes={null} />);
    expect(screen.queryByTestId("run-reserved-capacity")).not.toBeInTheDocument();
  });
});
