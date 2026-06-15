import { render, screen, within } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ProjectDetailPanel } from "../ProjectDetailPanel";
import type {
  ConfigAgent,
  ConfigProject,
  ConfigRepo,
  ConfigTracker,
  ResolvedSettings,
  TriggerSemantics,
} from "@/lib/configApi";

// p0271: the per-project detail SHEET. Renders the project's agent, repos (with
// full URLs), tracker (with how-it-tracks config), and resolved effective values
// (each with a quiet provenance hint; toolchain image must not fabricate a value).

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
      pollingIntervalSeconds: 120,
      commentKeyword: "@agentsmith",
      ...trigger,
    },
  };
}

const REPOS: ConfigRepo[] = [
  { name: "sample-server", type: "GitHub", url: "https://github.com/acme/sample-server.git", organization: "acme", project: null, defaultBranch: "main" },
];
const TRACKERS: ConfigTracker[] = [
  { name: "acme-jira", type: "Jira", url: "https://acme.atlassian.net", project: "OPS", openStates: ["To Do"], doneStatus: "Done" },
];
const AGENTS: ConfigAgent[] = [
  { name: "claude", type: "anthropic", model: "claude-opus-4-1", networkTimeoutSeconds: 300, maxFixIterations: 3, requestsPerMinute: 50, inputTokensPerMinute: 80000, maxConcurrentSkillRounds: 1 },
];

function renderPanel(project: ConfigProject) {
  return render(<ProjectDetailPanel project={project} repos={REPOS} trackers={TRACKERS} agents={AGENTS} />);
}

describe("ProjectDetailPanel", () => {
  it("ProjectDetailPanel_ShowsResolvedValuesWithProvenanceHints", () => {
    const resolved = makeResolved({ stepTimeoutSeconds: { value: 1200, source: "override" } });
    renderPanel(makeProject(resolved));

    const stepRow = screen.getByTestId("resolved-step-timeout");
    expect(stepRow).toHaveTextContent("1200s");
    expect(within(stepRow).getByText("override")).toBeInTheDocument();
    expect(within(screen.getByTestId("resolved-cost-cap")).getByText("default")).toBeInTheDocument();
  });

  it("ProjectDetailPanel_ToolchainImage_ShowsResolvedPerRun_NotAValue", () => {
    renderPanel(makeProject(makeResolved()));

    const row = screen.getByTestId("resolved-toolchain-image");
    expect(row).toHaveTextContent("resolved per run");
    expect(within(row).getByText("per run")).toBeInTheDocument();
    expect(row).not.toHaveTextContent("ghcr.io");
  });

  it("ProjectDetailPanel_RendersRepoUrlsAndTrackerTrackingConfig", () => {
    renderPanel(makeProject(makeResolved()));

    // Full repo URL is shown.
    const repo = screen.getByTestId("repo-sample-server");
    expect(repo).toHaveTextContent("https://github.com/acme/sample-server.git");

    // Tracker section shows how it tracks.
    const tracker = screen.getByTestId("trigger-semantics");
    expect(tracker).toHaveTextContent("acme.atlassian.net");
    expect(tracker).toHaveTextContent("triggers on");
    expect(tracker).toHaveTextContent("To Do");
    expect(tracker).toHaveTextContent("Done");
    expect(tracker).toHaveTextContent("Failed");
    expect(tracker).toHaveTextContent("every 120s");
    expect(tracker).toHaveTextContent("@agentsmith");
  });
});
