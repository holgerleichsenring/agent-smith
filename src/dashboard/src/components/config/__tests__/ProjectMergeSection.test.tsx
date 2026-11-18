import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProjectMergeSection } from "../ProjectMergeSection";
import { BranchCleanupForm, PreconditionsForm } from "../SweepSettingsForms";
import { AgentTuningTab } from "../agent/AgentTuningTab";
import { SETTING_KEYS } from "../settings";
import type { ConfigCapabilities, StudioAgent, StudioProject } from "@/lib/configApi";

// 2026-11-17-b34eb: the settings added on 2026-11-16/17, editable in the studio.

const capabilities = {
  trackerTypes: [], connectionTypes: [], agentProviders: [], resolutionStrategies: [], permissions: [],
  builtInRoles: [], pipelines: [], roles: [],
  mergeWhen: ["manual", "on_gate_green", "on_ci_green"],
  requiresStart: ["on_merge", "on_green_push"],
} as ConfigCapabilities;

const project: StudioProject = {
  id: "p", agent: "a", tracker: "t", repos: ["r"], pipeline: "", pipelines: [], resolution: null,
  requiresStart: "on_green_push",
};

function options(testId: string): string[] {
  return Array.from((screen.getByTestId(testId) as HTMLSelectElement).options).map((o) => o.value).filter(Boolean);
}

describe("ProjectMergeSection", () => {
  // 2026-11-17-3435d
  it("shows the CI loop's keys for on_ci_green and writes them", () => {
    const onChange = vi.fn();
    const ci = { ...project, merge: { when: "on_ci_green", respectBranchProtection: true, refreshOpenPullRequests: false } };
    render(<ProjectMergeSection project={ci} onChange={onChange} capabilities={capabilities} secrets={[{ id: "approver" } as never]} />);

    fireEvent.change(screen.getByTestId("form-field-merge-ciFixAttempts"), { target: { value: "5" } });
    expect(onChange.mock.calls.at(-1)![0].merge).toMatchObject({ when: "on_ci_green", ciFixAttempts: 5 });
    fireEvent.change(screen.getByTestId("form-field-merge-approvalAuth"), { target: { value: "approver" } });
    expect(onChange.mock.calls.at(-1)![0].merge.approvalAuth).toBe("approver");
  });

  it("hides the CI cadence for another mode but keeps the evidence bounds", () => {
    render(<ProjectMergeSection project={project} onChange={() => {}} capabilities={capabilities} />);
    expect(screen.queryByTestId("form-field-merge-ciPollSeconds")).toBeNull();
    expect(screen.getByTestId("form-field-merge-ciExcerptChars")).toBeTruthy();
  });

  it("offers the server's modes, a mode it gained included", () => {
    render(<ProjectMergeSection project={project} onChange={() => {}} capabilities={capabilities} />);
    expect(options("form-field-merge-when")).toEqual(["manual", "on_gate_green", "on_ci_green"]);
    expect(options("form-field-requiresStart")).toEqual(["on_merge", "on_green_push"]);
  });

  it("reads a project without a merge block as manual and writes the block once touched", () => {
    const onChange = vi.fn();
    render(<ProjectMergeSection project={project} onChange={onChange} capabilities={capabilities} />);
    expect((screen.getByTestId("form-field-merge-when") as HTMLSelectElement).value).toBe("manual");

    fireEvent.change(screen.getByTestId("form-field-merge-when"), { target: { value: "on_gate_green" } });

    expect(onChange).toHaveBeenLastCalledWith({
      ...project, merge: { when: "on_gate_green", respectBranchProtection: false, refreshOpenPullRequests: false },
    });
  });

  it("patches requires_start", () => {
    const onChange = vi.fn();
    render(<ProjectMergeSection project={project} onChange={onChange} capabilities={capabilities} />);
    fireEvent.change(screen.getByTestId("form-field-requiresStart"), { target: { value: "on_merge" } });
    expect(onChange).toHaveBeenLastCalledWith({ ...project, requiresStart: "on_merge" });
  });
});

describe("sweep settings", () => {
  it("are on the settings rail", () => {
    expect(SETTING_KEYS).toContain("preconditions");
    expect(SETTING_KEYS).toContain("branch_cleanup");
  });

  it("branch cleanup patches its interval", () => {
    const onChange = vi.fn();
    render(<BranchCleanupForm value={{ enabled: true, intervalSeconds: 86400 }} onChange={onChange} />);
    fireEvent.change(screen.getByTestId("setting-branch-cleanup-interval"), { target: { value: "3600" } });
    expect(onChange).toHaveBeenLastCalledWith({ enabled: true, intervalSeconds: 3600 });
  });

  it("preconditions patches its interval", () => {
    const onChange = vi.fn();
    render(<PreconditionsForm value={{ recheckIntervalSeconds: 300 }} onChange={onChange} />);
    fireEvent.change(screen.getByTestId("setting-preconditions-interval"), { target: { value: "120" } });
    expect(onChange).toHaveBeenLastCalledWith({ recheckIntervalSeconds: 120 });
  });
});

describe("agent compaction ratios", () => {
  it("patches the fold target ratio", () => {
    const onChange = vi.fn();
    const agent = {
      id: "a", provider: "openai", catalog: {}, models: {}, keySecret: null,
      compaction: { isEnabled: true, thresholdIterations: 8, maxContextTokens: 200000, keepRecentIterations: 3 },
    } as unknown as StudioAgent;
    render(<AgentTuningTab draft={agent} onChange={onChange} />);
    fireEvent.click(screen.getByTestId("agent-section-compaction").querySelector("button, summary") as Element);
    fireEvent.change(screen.getByTestId("form-field-compaction-foldTargetRatio"), { target: { value: "0.25" } });
    expect(onChange.mock.calls.at(-1)?.[0].compaction.foldTargetRatio).toBe(0.25);
  });
});
