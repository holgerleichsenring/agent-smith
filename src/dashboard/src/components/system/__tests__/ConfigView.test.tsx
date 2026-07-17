import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ConfigView } from "../ConfigView";
import type { ConfigProject, ConfigSnapshot } from "@/lib/configApi";
import { fetchConfig } from "@/lib/configApi";
import type { SubsystemActivity } from "@/hooks/useSubsystemActivity";

// p0345c: the config-reads page answers "is what RUNS what you CONFIGURED" —
// wiring chips + provenance summary + read freshness lead the page, a DRIFT
// banner fires when agentsmith.yml changed after the runtime's last read, and
// the raw read-events stream is collapsed at the bottom.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConfig: vi.fn(),
}));

const mockedFetch = vi.mocked(fetchConfig);

function rv<T>(value: T, source: "global-default" | "override" | "run-resolved" = "global-default") {
  return { value, source };
}

function project(name: string): ConfigProject {
  return {
    name,
    pipeline: "fix-bug",
    agentName: "azure_openai",
    trackerName: "azdo",
    repoNames: ["Sample.Server"],
    pipelines: ["fix-bug"],
    resolved: {
      stepTimeoutSeconds: rv(600, "override"),
      runCommandTimeoutSeconds: rv(120),
      sandboxResources: rv({ cpuRequest: "1", cpuLimit: "2", memoryRequest: "1Gi", memoryLimit: "2Gi" }),
      agentImage: rv("agent:1"),
      orchestratorImage: rv("orch:1"),
      toolchainImage: rv(null, "run-resolved"),
      costCap: rv({ usd: 5, tokens: 1_000_000 }, "override"),
      resolutionError: null,
    },
    trigger: {
      triggerStatuses: ["ready"],
      doneStatus: "done",
      failedStatus: "failed",
      pollingEnabled: true,
      pollingIntervalSeconds: 300,
      commentKeyword: null,
    },
  };
}

function snapshot(over: Partial<ConfigSnapshot> = {}): ConfigSnapshot {
  return {
    agents: [],
    repos: [],
    trackers: [],
    projects: [project("sample")],
    edges: [],
    globals: {
      sandbox: { agentRegistry: "r", agentVersion: "1", stepTimeoutSeconds: 600, runCommandTimeoutSeconds: 120 },
      orchestrator: { registry: "r", version: "1", maxRunWallTimeSeconds: 3600 },
      limits: { maxToolCallsPerSkill: 1, maxLlmCallsPerSkill: 1, maxConcurrentSkillCalls: 1, maxSubAgentsPerRun: 1 },
      costCap: { usd: 5, tokens: 1 },
      persistenceProvider: "postgres",
    },
    configPath: "/etc/agentsmith/agentsmith.yml",
    fileModifiedAt: null,
    lastReadAt: null,
    ...over,
  };
}

const activity: SubsystemActivity = {
  id: "config",
  label: "Config file reads",
  live: false,
  freshness: "—",
  tail: null,
  events: [],
};

beforeEach(() => mockedFetch.mockReset());

describe("ConfigView drift story (p0345c)", () => {
  it("ConfigReads_DriftBanner_WhenFileNewerThanLastRead", async () => {
    mockedFetch.mockResolvedValue(
      snapshot({
        fileModifiedAt: "2026-07-17T10:05:00Z",
        lastReadAt: "2026-07-17T10:00:00Z",
      }),
    );
    render(<ConfigView activity={activity} />);

    const banner = await screen.findByTestId("config-drift-banner");
    expect(banner.className).toContain("banner");
    expect(banner.className).toContain("wait");
    expect(banner).toHaveTextContent(
      "agentsmith.yml changed after the last read — the runtime may be stale",
    );
    expect(banner).toHaveTextContent("/etc/agentsmith/agentsmith.yml");
  });

  it("ConfigReads_NoDrift_NoBanner_FreshnessShown", async () => {
    mockedFetch.mockResolvedValue(
      snapshot({
        fileModifiedAt: "2026-07-17T09:00:00Z",
        lastReadAt: new Date(Date.now() - 42_000).toISOString(),
      }),
    );
    render(<ConfigView activity={activity} />);

    await screen.findByTestId("config-wiring-sample");
    expect(screen.queryByTestId("config-drift-banner")).toBeNull();
    // The freshness line: the runtime's last actual read, humanized.
    expect(screen.getByTestId("config-read-freshness")).toHaveTextContent(/read \d+s ago/);
  });

  it("ConfigReads_WiringChips_AndProvenanceSummary_PerProject", async () => {
    mockedFetch.mockResolvedValue(snapshot());
    render(<ConfigView activity={activity} />);

    const wire = await screen.findByTestId("config-wiring-sample");
    expect(wire.className).toContain("wire");
    expect(screen.getByTestId("config-wiring-agent-sample")).toHaveTextContent("azure_openai");
    expect(screen.getByTestId("config-wiring-tracker-sample")).toHaveTextContent("azdo");
    expect(screen.getByTestId("config-wiring-repo-sample-Sample.Server")).toHaveTextContent("Sample.Server");
    // 2 overrides, 4 global defaults, 1 run-resolved in the fixture.
    expect(screen.getByTestId("config-provenance-sample")).toHaveTextContent(
      "2 explicit · 4 default · 1 per-run",
    );
    // No read recorded yet → the freshness line says so instead of faking an age.
    expect(screen.getByTestId("config-read-freshness")).toHaveTextContent("no config read recorded");
  });

  it("ConfigReads_RawStream_CollapsedUntilToggled", async () => {
    mockedFetch.mockResolvedValue(snapshot());
    render(<ConfigView activity={activity} />);
    await screen.findByTestId("config-wiring-sample");

    // Collapsed by default — the stream is the appendix, not the story.
    expect(screen.queryByTestId("subsystem-detail-config")).toBeNull();
    fireEvent.click(screen.getByTestId("config-stream-toggle"));
    expect(screen.getByTestId("subsystem-detail-config")).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("config-stream-toggle"));
    expect(screen.queryByTestId("subsystem-detail-config")).toBeNull();
  });

  it("ConfigReads_EmptyProjects_HonestState", async () => {
    mockedFetch.mockResolvedValue(snapshot({ projects: [] }));
    render(<ConfigView activity={activity} />);
    expect(await screen.findByTestId("config-view-empty")).toHaveTextContent("No projects configured.");
  });
});
