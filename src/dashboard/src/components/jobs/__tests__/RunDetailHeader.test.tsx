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
  runActive: false,
  cancelRequested: false,
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

  it("p0243: shows a cancel button only while the run is active", () => {
    const { rerender } = render(<RunDetailHeader {...base} runActive={false} />);
    expect(screen.queryByTestId("cancel-run-run-abc")).not.toBeInTheDocument();
    rerender(<RunDetailHeader {...base} runActive={true} />);
    expect(screen.getByTestId("cancel-run-run-abc")).toBeInTheDocument();
  });
});
