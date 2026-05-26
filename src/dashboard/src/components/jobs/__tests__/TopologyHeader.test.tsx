import { render, screen } from "@testing-library/react";
import { TopologyHeader } from "../TopologyHeader";
import type { RunMeta } from "@/lib/api";

function meta(overrides: Partial<RunMeta>): RunMeta {
  return {
    runId: "2026-05-20T22-27-43-8a3f",
    pipelineName: "fix-bug",
    status: "done",
    startedAt: "2026-05-20T22:27:43Z",
    durationSeconds: 120,
    repoMode: "mono",
    sandboxCount: 1,
    repos: ["primary"],
    ticket: null,
    type: "fix",
    ...overrides,
  };
}

describe("TopologyHeader", () => {
  it("renders multi-repo badge and repo names", () => {
    render(<TopologyHeader meta={meta({ repoMode: "multi", repos: ["api", "web", "worker"], sandboxCount: 3 })} />);
    expect(screen.getByTestId("topology-header")).toBeInTheDocument();
    const summary = screen.getByTestId("topology-summary");
    expect(summary).toHaveTextContent(/multi-repo \(3\)/);
    expect(summary).toHaveTextContent(/3 sandboxes/);
    expect(screen.getByText("api")).toBeInTheDocument();
    expect(screen.getByText("web")).toBeInTheDocument();
    expect(screen.getByText("worker")).toBeInTheDocument();
  });

  it("renders fallback gracefully for unknown topology", () => {
    render(<TopologyHeader meta={meta({ repoMode: "unknown", repos: [], sandboxCount: 0, pipelineName: "unknown", status: "unknown" })} />);
    const summary = screen.getByTestId("topology-summary");
    expect(summary).toHaveTextContent(/unknown · 0 sandboxes · unknown · unknown/);
  });
});
