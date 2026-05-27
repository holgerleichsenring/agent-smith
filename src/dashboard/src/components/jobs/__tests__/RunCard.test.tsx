import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { RunCard } from "../RunCard";
import type { RunSnapshot } from "@/types/hub-events";

const baseSnapshot: RunSnapshot = {
  runId: "2026-05-27T10-00-00-aaaa",
  pipeline: "fix-bug",
  trigger: "ticket",
  repos: ["server"],
  status: "running",
  prUrl: null,
  summary: null,
  startedAt: new Date(Date.now() - 30_000).toISOString(),
  finishedAt: null,
  sandboxes: 1,
  stepIndex: 3,
  stepName: "AnalyzeCode",
  totalSteps: 10,
  lastEventType: "StepStarted",
};

describe("RunCard", () => {
  it("renders pipeline + repos label + step progress", () => {
    render(<RunCard snapshot={baseSnapshot} />);
    expect(screen.getByText("fix-bug")).toBeInTheDocument();
    expect(screen.getByText("server")).toBeInTheDocument();
    expect(screen.getByText("step 3/10")).toBeInTheDocument();
  });

  it("collapses multi-repo to a count label", () => {
    render(<RunCard snapshot={{ ...baseSnapshot, repos: ["api", "worker", "web"] }} />);
    expect(screen.getByText("3 repos")).toBeInTheDocument();
  });

  it("renders status badge for success", () => {
    render(<RunCard snapshot={{ ...baseSnapshot, status: "success" }} />);
    expect(screen.getByText("success")).toBeInTheDocument();
  });
});
