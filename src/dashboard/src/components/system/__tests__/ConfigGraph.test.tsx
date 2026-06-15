import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ConfigGraph } from "../ConfigGraph";
import type { ConfigEdge, ConfigProject, ResolvedSettings, TriggerSemantics } from "@/lib/configApi";

// p0266/p0270b: the config-time relationship graph. Root → project columns →
// each project's linked entities. p0270b adds project selection: clicking a
// project node selects it and dims the rest.

const RESOLVED: ResolvedSettings = {
  stepTimeoutSeconds: { value: 900, source: "global-default" },
  runCommandTimeoutSeconds: { value: 300, source: "global-default" },
  sandboxResources: { value: { cpuRequest: "1", cpuLimit: "2", memoryRequest: "2Gi", memoryLimit: "4Gi" }, source: "global-default" },
  agentImage: { value: "ghcr.io/acme/agent:0.48.0", source: "global-default" },
  orchestratorImage: { value: "ghcr.io/acme/orch:0.49.0", source: "global-default" },
  toolchainImage: { value: null, source: "run-resolved" },
  costCap: { value: { usd: 5, tokens: 500000 }, source: "global-default" },
  resolutionError: null,
};

const TRIGGER: TriggerSemantics = {
  triggerStatuses: ["To Do"],
  doneStatus: "Done",
  failedStatus: "Failed",
  pollingEnabled: true,
};

const PROJECTS: ConfigProject[] = [
  { name: "ops", pipeline: "fix-bug", agentName: "claude", trackerName: "jira", repoNames: ["repo-a"], pipelines: ["fix-bug"], resolved: RESOLVED, trigger: TRIGGER },
];

const EDGES: ConfigEdge[] = [
  { from: "ops", to: "claude", kind: "agent" },
  { from: "ops", to: "jira", kind: "tracker" },
  { from: "ops", to: "repo-a", kind: "repo" },
  { from: "ops", to: "fix-bug", kind: "pipeline" },
];

describe("ConfigGraph", () => {
  it("ConfigGraph_WithProjects_RendersNodesAndEdges", () => {
    render(<ConfigGraph projects={PROJECTS} edges={EDGES} selected={null} onSelectProject={() => {}} />);

    expect(screen.getByTestId("config-graph")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-root")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-project-ops")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-agent-claude")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-tracker-jira")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-repo-repo-a")).toBeInTheDocument();
    expect(screen.getByTestId("config-node-pipeline-fix-bug")).toBeInTheDocument();

    // 1 root→project + 4 project→entity edges.
    expect(screen.getAllByTestId("config-edge")).toHaveLength(5);
  });

  it("ConfigGraph_NoProjects_RendersEmptyState", () => {
    render(<ConfigGraph projects={[]} edges={[]} selected={null} onSelectProject={() => {}} />);

    expect(screen.getByTestId("config-graph-empty")).toBeInTheDocument();
    expect(screen.queryByTestId("config-graph")).not.toBeInTheDocument();
  });

  it("ConfigGraph_ClickProject_SelectsAndHighlightsSubgraph", () => {
    const onSelectProject = vi.fn();
    const { rerender } = render(
      <ConfigGraph projects={PROJECTS} edges={EDGES} selected={null} onSelectProject={onSelectProject} />,
    );

    fireEvent.click(screen.getByTestId("config-node-project-ops"));
    expect(onSelectProject).toHaveBeenCalledWith("ops");

    rerender(<ConfigGraph projects={PROJECTS} edges={EDGES} selected="ops" onSelectProject={onSelectProject} />);
    expect(screen.getByTestId("config-node-project-ops")).toHaveAttribute("data-selected", "true");
  });
});
