import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProjectMergeSection } from "../ProjectMergeSection";
import { BranchCleanupForm, HoldsForm, PreconditionsForm } from "../SweepSettingsForms";
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
    render(<ProjectMergeSection project={ci} onChange={onChange} capabilities={capabilities} />);

    fireEvent.change(screen.getByTestId("form-field-merge-ciFixAttempts"), { target: { value: "5" } });
    expect(onChange.mock.calls.at(-1)![0].merge).toMatchObject({ when: "on_ci_green", ciFixAttempts: 5 });
    // 2026-11-25-9992h: nothing approves, so the form offers no approval identity.
    expect(screen.queryByTestId("form-field-merge-approvalAuth")).toBeNull();
  });

  // 2026-11-23-849e: the poll and the pending bound re-read every project's pending CI, so they show for every mode.
  it("hides the merge-on-green keys for another mode but keeps the poll and the evidence bounds", () => {
    const manual = { ...project, merge: { when: "manual", respectBranchProtection: false, refreshOpenPullRequests: false, seriesWhen: "manual" } };
    render(<ProjectMergeSection project={manual} onChange={() => {}} capabilities={capabilities} />);
    expect(screen.queryByTestId("form-field-merge-ciSettleSeconds")).toBeNull();
    expect(screen.getByTestId("form-field-merge-ciPollSeconds")).toBeTruthy();
    expect(screen.getByTestId("form-field-merge-ciPendingSeconds")).toBeTruthy();
    expect(screen.getByTestId("form-field-merge-ciExcerptChars")).toBeTruthy();
  });

  // 2026-11-24-6811c: the fix budget also bounds a base refresh's repair passes; 2026-11-24-297b: a conflicting pull request
  // is refreshed in every project, so a manual project without the refresh setting shows it too.
  it("shows the fix attempts in any mode", () => {
    const manual = { ...project, merge: { when: "manual", respectBranchProtection: false, refreshOpenPullRequests: false, seriesWhen: "manual" } };
    render(<ProjectMergeSection project={manual} onChange={() => {}} capabilities={capabilities} />);
    expect(screen.getByTestId("form-field-merge-ciFixAttempts")).toBeTruthy();
    expect(screen.queryByTestId("form-field-merge-ciSettleSeconds")).toBeNull();
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
      ...project, merge: { when: "on_gate_green", respectBranchProtection: false, refreshOpenPullRequests: false, seriesWhen: "on_ci_green" },
    });
  });

  // 2026-11-24-f9c5a
  it("shows series_when beside merge.when, on_ci_green by default, writes it, and shows the CI keys for it", () => {
    const onChange = vi.fn();
    const manual = { ...project, merge: { when: "manual", respectBranchProtection: false, refreshOpenPullRequests: false } };
    render(<ProjectMergeSection project={manual} onChange={onChange} capabilities={capabilities} />);
    expect((screen.getByTestId("form-field-merge-seriesWhen") as HTMLSelectElement).value).toBe("on_ci_green");
    expect(screen.getByTestId("form-field-merge-ciSettleSeconds")).toBeTruthy();

    fireEvent.change(screen.getByTestId("form-field-merge-seriesWhen"), { target: { value: "manual" } });

    expect(onChange.mock.calls.at(-1)![0].merge).toMatchObject({ when: "manual", seriesWhen: "manual" });
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
    render(<PreconditionsForm value={{ recheckIntervalSeconds: 300, interruptedRestartWindowMinutes: 60 }} onChange={onChange} />);
    fireEvent.change(screen.getByTestId("setting-preconditions-interval"), { target: { value: "120" } });
    expect(onChange).toHaveBeenLastCalledWith({ recheckIntervalSeconds: 120, interruptedRestartWindowMinutes: 60 });
  });

  // 2026-11-22-747dc: the interrupted-run restart window, 0 turning it off.
  it("preconditions patches its restart window", () => {
    const onChange = vi.fn();
    render(<PreconditionsForm value={{ recheckIntervalSeconds: 300, interruptedRestartWindowMinutes: 60 }} onChange={onChange} />);
    expect(screen.getByText("Restart interrupted runs (minutes before start)")).toBeTruthy();
    fireEvent.change(screen.getByTestId("setting-preconditions-restart-window"), { target: { value: "0" } });
    expect(onChange).toHaveBeenLastCalledWith({ recheckIntervalSeconds: 300, interruptedRestartWindowMinutes: 0 });
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

// 2026-11-23-31dci: holds.escalate_after_minutes has its field, 0 turning the report off.
describe("holds setting", () => {
  it("holds patches its escalation minutes", () => {
    const onChange = vi.fn();
    render(<HoldsForm value={{ escalateAfterMinutes: 60 }} onChange={onChange} />);
    expect(screen.getByText("Report a wait to the admin after (minutes)")).toBeTruthy();
    fireEvent.change(screen.getByTestId("setting-holds-escalate"), { target: { value: "0" } });
    expect(onChange).toHaveBeenLastCalledWith({ escalateAfterMinutes: 0 });
    expect(SETTING_KEYS).toContain("holds");
  });
});
