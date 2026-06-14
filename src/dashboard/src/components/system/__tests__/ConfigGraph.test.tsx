import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ConfigGraph } from "../ConfigGraph";
import type { ConfigEdge, ConfigProject } from "@/lib/configApi";

// p0266: the config-time relationship graph. Root → project columns → each
// project's linked entities, one edge per (project → entity) plus root → project.

const PROJECTS: ConfigProject[] = [
  { name: "ops", pipeline: "fix-bug", agentName: "claude", trackerName: "jira", repoNames: ["repo-a"], pipelines: ["fix-bug"] },
];

const EDGES: ConfigEdge[] = [
  { from: "ops", to: "claude", kind: "agent" },
  { from: "ops", to: "jira", kind: "tracker" },
  { from: "ops", to: "repo-a", kind: "repo" },
  { from: "ops", to: "fix-bug", kind: "pipeline" },
];

describe("ConfigGraph", () => {
  it("ConfigGraph_WithProjects_RendersNodesAndEdges", () => {
    render(<ConfigGraph projects={PROJECTS} edges={EDGES} />);

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
    render(<ConfigGraph projects={[]} edges={[]} />);

    expect(screen.getByTestId("config-graph-empty")).toBeInTheDocument();
    expect(screen.queryByTestId("config-graph")).not.toBeInTheDocument();
  });
});
