import { render, screen, within } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ProjectDetailPanel } from "../ProjectDetailPanel";
import type { ConfigProject, ResolvedSettings, TriggerSemantics } from "@/lib/configApi";

// p0270b: the per-project explainer. Each resolved effective value carries a
// provenance badge (default vs override vs per run); the toolchain image is
// knowable only per run so it must NOT fabricate a value; the tracker states
// are labelled by role (triggers on / done / failed / polling).

function makeResolved(overrides: Partial<ResolvedSettings> = {}): ResolvedSettings {
  return {
    stepTimeoutSeconds: { value: 900, source: "global-default" },
    runCommandTimeoutSeconds: { value: 300, source: "global-default" },
    sandboxResources: { value: { cpuRequest: "1", cpuLimit: "2", memoryRequest: "2Gi", memoryLimit: "4Gi" }, source: "global-default" },
    agentImage: { value: "ghcr.io/acme/agent:0.48.0", source: "global-default" },
    orchestratorImage: { value: "ghcr.io/acme/orch:0.49.0", source: "global-default" },
    toolchainImage: { value: null, source: "run-resolved" },
    costCap: { value: { usd: 5, tokens: 500000 }, source: "global-default" },
    resolutionError: null,
    ...overrides,
  };
}

function makeProject(resolved: ResolvedSettings, trigger?: Partial<TriggerSemantics>): ConfigProject {
  return {
    name: "ops",
    pipeline: "fix-bug",
    agentName: "claude",
    trackerName: "acme-jira",
    repoNames: ["sample-server"],
    pipelines: ["fix-bug"],
    resolved,
    trigger: {
      triggerStatuses: ["To Do"],
      doneStatus: "Done",
      failedStatus: "Failed",
      pollingEnabled: true,
      ...trigger,
    },
  };
}

describe("ProjectDetailPanel", () => {
  it("ProjectDetailPanel_ShowsResolvedValuesWithProvenanceBadges", () => {
    const resolved = makeResolved({
      stepTimeoutSeconds: { value: 1200, source: "override" },
      costCap: { value: { usd: 5, tokens: 500000 }, source: "global-default" },
    });
    render(<ProjectDetailPanel project={makeProject(resolved)} />);

    const stepRow = screen.getByTestId("resolved-step-timeout");
    expect(stepRow).toHaveTextContent("1200s");
    expect(within(stepRow).getByText("override")).toBeInTheDocument();

    const costRow = screen.getByTestId("resolved-cost-cap");
    expect(within(costRow).getByText("default")).toBeInTheDocument();
  });

  it("ProjectDetailPanel_ToolchainImage_ShowsResolvedPerRun_NotAValue", () => {
    const resolved = makeResolved({ toolchainImage: { value: null, source: "run-resolved" } });
    render(<ProjectDetailPanel project={makeProject(resolved)} />);

    const row = screen.getByTestId("resolved-toolchain-image");
    expect(row).toHaveTextContent("resolved per run");
    expect(within(row).getByText("per run")).toBeInTheDocument();
    expect(row).not.toHaveTextContent("ghcr.io");
  });

  it("ProjectDetailPanel_TrackerStates_LabelledByRole", () => {
    render(<ProjectDetailPanel project={makeProject(makeResolved())} />);

    const trigger = screen.getByTestId("trigger-semantics");
    expect(trigger).toHaveTextContent("triggers on");
    expect(trigger).toHaveTextContent("done");
    expect(trigger).toHaveTextContent("failed");
    expect(trigger).toHaveTextContent("To Do");
    expect(trigger).toHaveTextContent("Done");
    expect(trigger).toHaveTextContent("Failed");
  });
});
