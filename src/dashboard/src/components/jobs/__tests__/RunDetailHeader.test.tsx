import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { RunDetailHeader } from "../RunDetailHeader";

const base = {
  pipeline: "fix-bug",
  ticketId: "123",
  ticketTitle: "Login button broken",
  runId: "run-abc",
  stepCaption: null,
  agentName: null,
  repoNames: [],
  connectionState: HubConnectionState.Connected,
  status: "success",
  cancelRequested: false,
  costUsd: null,
  reservedGiMinutes: null,
};

describe("RunDetailHeader", () => {
  it("RunDetail_Heading_ShowsPipelineName_NotTicketTitle", () => {
    render(<RunDetailHeader {...base} />);
    const h1 = screen.getByTestId("run-heading");
    expect(h1).toHaveTextContent("fix-bug");
    expect(h1).not.toHaveTextContent("Login button broken");
  });

  it("RunDetail_TicketIdAndTitle_RenderAsSecondaryMetadata", () => {
    render(<RunDetailHeader {...base} />);
    expect(screen.getByTestId("run-ticket-id")).toHaveTextContent("#123");
    expect(screen.getByTestId("run-ticket-title")).toHaveTextContent("Login button broken");
    // The ticket title is metadata, never the headline.
    expect(screen.getByTestId("run-heading")).not.toHaveTextContent("Login button broken");
  });

  it("RunDetail_NoPipeline_FallsBackToRunLabel", () => {
    render(<RunDetailHeader {...base} pipeline={null} />);
    expect(screen.getByTestId("run-heading")).toHaveTextContent("run");
  });

  it("RunDetail_CancelButton_ShownForRunningAndQueued_NotTerminal", () => {
    // p0330: cancellable states are running AND queued (the capacity-waiting
    // run is exactly the one the operator most wants to kill); terminal
    // statuses drop the button.
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
    // p0330: the durable flag outlives the button — a run that ended
    // success/failed before the cancel was enforced shows the muted hint;
    // a run that reached "cancelled" shows nothing extra.
    render(<RunDetailHeader {...base} status="failed" cancelRequested={true} />);
    expect(screen.getByTestId("cancel-requested-hint")).toHaveTextContent("cancel was requested");
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
  });

  it("RunDetail_Cancelled_ShowsNoCancelRequestedIndicator", () => {
    render(<RunDetailHeader {...base} status="cancelled" cancelRequested={true} />);
    expect(screen.queryByTestId("cancel-requested-badge")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cancel-requested-hint")).not.toBeInTheDocument();
  });

  it("RunDetail_ReservedCapacity_RendersNextToCost_LabeledAsReserved", () => {
    // p0332: resource-time renders beside the LLM cost so the operator sees WHY
    // a run was expensive — tokens or pods. The figure is a RESERVATION
    // (memory request × pod lifetime): the label must say "reserved" and the
    // value must never be dressed up as money.
    render(<RunDetailHeader {...base} costUsd={0.07} reservedGiMinutes={6.2} />);
    const line = screen.getByTestId("run-cost-line");
    expect(line).toHaveTextContent("$0.07 LLM");
    const reserved = screen.getByTestId("run-reserved-capacity");
    expect(reserved).toHaveTextContent("reserved 6.2 Gi·min");
    // Honest label: no dollar sign anywhere near the reserved figure.
    expect(reserved.textContent).not.toContain("$");
  });

  it("RunDetail_NoReservedFigure_OmitsReservedLabel", () => {
    // Null = not computable (running run, pre-p0332 row) — show nothing
    // rather than a fake zero.
    render(<RunDetailHeader {...base} costUsd={0.07} reservedGiMinutes={null} />);
    expect(screen.getByTestId("run-cost-line")).toBeInTheDocument();
    expect(screen.queryByTestId("run-reserved-capacity")).not.toBeInTheDocument();
  });

  it("RunDetail_NoCostNoReserved_OmitsCostLine", () => {
    render(<RunDetailHeader {...base} costUsd={null} reservedGiMinutes={null} />);
    expect(screen.queryByTestId("run-cost-line")).not.toBeInTheDocument();
  });
});
