import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TopologyCard } from "../TopologyCard";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";

const snapshot: RunSnapshot = {
  runId: "2026-05-27T10-00-00-aaaa",
  pipeline: "fix-bug",
  trigger: "ticket",
  repos: ["server"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: new Date().toISOString(),
  finishedAt: null,
  sandboxes: 1,
  stepIndex: 2,
  stepName: "CheckoutSource",
  totalSteps: 10,
  lastEventType: "StepStarted",
};

function stepStarted(idx: number, name: string): RunEvent {
  return {
    type: EventType.StepStarted,
    runId: snapshot.runId,
    timestamp: new Date().toISOString(),
    stepIndex: idx,
    stepName: name,
    totalSteps: 10,
  } as RunEvent;
}

function stepFinished(idx: number, status: string): RunEvent {
  return {
    type: EventType.StepFinished,
    runId: snapshot.runId,
    timestamp: new Date().toISOString(),
    stepIndex: idx,
    status,
    durationMs: 500,
  } as RunEvent;
}

describe("TopologyCard", () => {
  it("renders header with pipeline + runId + step counter", () => {
    render(<TopologyCard runId={snapshot.runId} snapshot={snapshot} events={[]} />);
    expect(screen.getByText("fix-bug")).toBeInTheDocument();
    expect(screen.getByText(snapshot.runId)).toBeInTheDocument();
    expect(screen.getByText(/server.*step 2\/10/)).toBeInTheDocument();
  });

  it("renders one row per started step with status reflecting finish event", () => {
    const events: RunEvent[] = [
      stepStarted(1, "CheckoutSource"),
      stepFinished(1, "success"),
      stepStarted(2, "AnalyzeCode"),
    ];
    render(<TopologyCard runId={snapshot.runId} snapshot={snapshot} events={events} />);
    const list = screen.getByTestId("step-progress-list");
    expect(list).toBeInTheDocument();
    expect(screen.getByText("CheckoutSource")).toBeInTheDocument();
    expect(screen.getByText("AnalyzeCode")).toBeInTheDocument();
  });

  it("shows empty-state when no step events have arrived yet", () => {
    render(<TopologyCard runId={snapshot.runId} snapshot={snapshot} events={[]} />);
    expect(screen.getByTestId("steps-empty")).toBeInTheDocument();
  });
});
