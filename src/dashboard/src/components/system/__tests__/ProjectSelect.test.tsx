import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ProjectSelect } from "../ProjectSelect";
import type { ConfigProject } from "@/lib/configApi";

// p0271: the project combobox that replaced the topology graph.

function project(name: string): ConfigProject {
  return {
    name, pipeline: "fix-bug", agentName: "claude", trackerName: "jira",
    repoNames: [], pipelines: ["fix-bug"],
    resolved: {
      stepTimeoutSeconds: { value: 900, source: "global-default" },
      runCommandTimeoutSeconds: { value: 300, source: "global-default" },
      sandboxResources: { value: { cpuRequest: "1", cpuLimit: "2", memoryRequest: "2Gi", memoryLimit: "4Gi" }, source: "global-default" },
      agentImage: { value: "a", source: "global-default" },
      orchestratorImage: { value: "o", source: "global-default" },
      toolchainImage: { value: null, source: "run-resolved" },
      costCap: { value: { usd: 5, tokens: 500000 }, source: "global-default" },
      resolutionError: null,
    },
    trigger: { triggerStatuses: [], doneStatus: null, failedStatus: null, pollingEnabled: false, pollingIntervalSeconds: 60, commentKeyword: null },
  };
}

describe("ProjectSelect", () => {
  it("ProjectSelect_ChangesSelectedProject", () => {
    const onSelect = vi.fn();
    render(<ProjectSelect projects={[project("ops"), project("web")]} selected="ops" onSelect={onSelect} />);

    fireEvent.change(screen.getByTestId("project-select"), { target: { value: "web" } });

    expect(onSelect).toHaveBeenCalledWith("web");
  });
});
