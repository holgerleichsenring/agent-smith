import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ConfigDetail } from "../ConfigDetail";
import type { ConfigSnapshot } from "@/lib/configApi";

// p0266: the structured detail beneath the graph — global defaults plus the
// redacted agent / repo / tracker entries. Prop-driven, so it renders from a
// plain snapshot fixture with no live fetch.

const SNAPSHOT: ConfigSnapshot = {
  agents: [
    { name: "claude", type: "anthropic", model: "claude-opus-4-1", networkTimeoutSeconds: 300, maxFixIterations: 3, requestsPerMinute: 50, inputTokensPerMinute: 80000, maxConcurrentSkillRounds: 1 },
  ],
  repos: [{ name: "sample-server", type: "GitHub", host: "github.com", defaultBranch: "main" }],
  trackers: [{ name: "acme-jira", type: "Jira", project: "OPS", openStates: ["To Do"], doneStatus: "Done" }],
  projects: [{
    name: "ops",
    pipeline: "fix-bug",
    agentName: "claude",
    trackerName: "acme-jira",
    repoNames: ["sample-server"],
    pipelines: ["fix-bug"],
    resolved: {
      stepTimeoutSeconds: { value: 900, source: "global-default" },
      runCommandTimeoutSeconds: { value: 300, source: "global-default" },
      sandboxResources: { value: { cpuRequest: "1", cpuLimit: "2", memoryRequest: "2Gi", memoryLimit: "4Gi" }, source: "global-default" },
      agentImage: { value: "ghcr.io/acme/agent:0.48.0", source: "global-default" },
      orchestratorImage: { value: "ghcr.io/acme/orch:0.49.0", source: "global-default" },
      toolchainImage: { value: null, source: "run-resolved" },
      costCap: { value: { usd: 5, tokens: 500000 }, source: "global-default" },
      resolutionError: null,
    },
    trigger: { triggerStatuses: ["To Do"], doneStatus: "Done", failedStatus: "Failed", pollingEnabled: true },
  }],
  edges: [],
  globals: {
    sandbox: { agentRegistry: "ghcr.io/acme", agentVersion: "0.48.0", stepTimeoutSeconds: 900, runCommandTimeoutSeconds: 300 },
    orchestrator: { registry: "ghcr.io/acme", version: "0.49.0", maxRunWallTimeSeconds: 1800 },
    limits: { maxToolCallsPerSkill: 30, maxLlmCallsPerSkill: 15, maxConcurrentSkillCalls: 10, maxSubAgentsPerRun: 20 },
    costCap: { usd: 5, tokens: 500000 },
    persistenceProvider: "postgresql",
  },
};

describe("ConfigDetail", () => {
  it("ConfigDetail_Globals_RendersSandboxOrchestratorLimitCards", () => {
    render(<ConfigDetail config={SNAPSHOT} />);

    expect(screen.getByTestId("config-global-sandbox")).toHaveTextContent("0.48.0");
    expect(screen.getByTestId("config-global-sandbox")).toHaveTextContent("900s");
    expect(screen.getByTestId("config-global-orchestrator")).toHaveTextContent("1800s");
    expect(screen.getByTestId("config-global-limits")).toHaveTextContent("30");
    expect(screen.getByTestId("config-global-costcap")).toHaveTextContent("$5");
    expect(screen.getByTestId("config-global-persistence")).toHaveTextContent("postgresql");
  });

  it("ConfigDetail_RendersRedactedAgentRepoTracker", () => {
    render(<ConfigDetail config={SNAPSHOT} />);

    expect(screen.getByTestId("config-agent-claude")).toHaveTextContent("claude-opus-4-1");
    expect(screen.getByTestId("config-repo-sample-server")).toHaveTextContent("github.com");
    expect(screen.getByTestId("config-tracker-acme-jira")).toHaveTextContent("Done");
  });
});
