import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor, within, cleanup } from "@testing-library/react";
import { SettingsGroupStudio } from "../SettingsGroupStudio";
import { SettingsStudio } from "../SettingsStudio";
import { resetCapabilitiesCache } from "../useCapabilities";
import { SETTING_GROUPS } from "../settings";
import type { SettingKey } from "@/lib/configApi";

// 2026-11-28-9bd1: a settings group page — its former pages as collapsible sections that open and
// count what differs from the default, a filter to the changed ones, and one Save for the group.

const DEFAULTS: Record<string, unknown> = {
  orchestrator: { maxRunWallTimeSeconds: 1800 },
  primary_provider: { value: null },
  limits: {
    maxToolCallsPerSkill: 30, maxSecondsPerSkillCall: 300, maxConcurrentSubAgents: 4, maxSubAgentsPerRun: 20,
    maxSubAgentsPerDialogTurn: 4,
  },
  pipeline_cost_cap: {
    default: { usd: 5, tokens: 500000 },
    perPipeline: {},
    perTier: { Trivial: { usd: 1, tokens: 200000 }, Large: { usd: 25, tokens: 5000000 } },
  },
  queue: { maxParallelJobs: 4, consumeBlockSeconds: 5, shutdownGraceSeconds: 30, redisRetryIntervalSeconds: 10 },
  deployment: { registry: "", version: "" },
  dialogue: {
    hotWaitSeconds: 60, approvalTimeoutSeconds: 3600, dashboardUrl: null, adminAgent: null, adminReadLimit: 50,
    adminResultMaxChars: 8000, mockMaxChars: 200000, mockVersionsPerSpec: 10, mockVersionsPerConversation: 30,
    seriesMasterBudgetUsd: 5, seriesMasterBudgetAskUsd: 1, seriesMasterThreadMaxChars: 60000,
    seriesMasterThreadKeepTurns: 6, seriesStatusMinutes: 10, seriesWakeSettleSeconds: 20, questionDefaultMinutes: 60,
  },
  spec_review: { regroundReadBudget: 24 },
  preconditions: { recheckIntervalSeconds: 300, interruptedRestartWindowMinutes: 60 },
  holds: { escalateAfterMinutes: 0 },
  branch_cleanup: { enabled: false, intervalSeconds: 3600 },
  pipeline_data_flow: { enforce: false },
  trace: { enabled: false },
};

// What is saved: two values of limits and the deployment pin differ; everything else is its default.
const SAVED: Record<string, unknown> = {
  ...DEFAULTS,
  limits: { ...(DEFAULTS.limits as object), maxToolCallsPerSkill: 60, maxConcurrentSubAgents: 8 },
  deployment: { registry: "ghcr.io/example", version: "" },
};

const saveSetting = vi.fn((_key: string, value: unknown): Promise<unknown> => Promise.resolve(value));
const fetchSettingDefault = vi.fn((key: string) => Promise.resolve(DEFAULTS[key]));

const fetchSetting = vi.fn((key: string): Promise<unknown> => Promise.resolve(SAVED[key]));

vi.mock("@/lib/configApi", () => ({
  fetchSetting: (key: string) => fetchSetting(key),
  fetchSettingDefault: (key: string) => fetchSettingDefault(key),
  saveSetting: (key: string, value: unknown) => saveSetting(key, value),
  fetchCapabilities: vi.fn().mockResolvedValue({
    trackerTypes: [], connectionTypes: [], agentProviders: ["claude", "openai"], resolutionStrategies: [],
    permissions: [], builtInRoles: [], pipelines: ["fix-bug"], roles: [],
  }),
}));

beforeEach(() => {
  vi.clearAllMocks();
  resetCapabilitiesCache();
});

// The whole suite runs many files at once; a section waits on two fetches and its form.
const SLOW = { timeout: 5000 };

const section = (key: SettingKey) => screen.getByTestId(`settings-section-${key}`);
const toggle = (key: SettingKey) => screen.getByTestId(`settings-section-${key}-toggle`);

async function loaded(groupId: "runs" | "workflow" | "diagnostics", open: SettingKey | null = null) {
  render(<SettingsGroupStudio groupId={groupId} open={open} />);
  const members = SETTING_GROUPS.find((g) => g.id === groupId)!.members;
  await waitFor(() => expect(fetchSettingDefault).toHaveBeenCalledTimes(members.length), SLOW);
  return members;
}

describe("SettingsGroupStudio", () => {
  it("Section_WithAChange_OpensAndSaysNChanged", async () => {
    await loaded("runs");
    expect(await screen.findByTestId("settings-section-limits-changed", {}, SLOW)).toHaveTextContent("2 changed");
    expect(screen.getByTestId("settings-section-deployment-changed")).toHaveTextContent("1 changed");
    expect(section("limits")).toHaveAttribute("data-open", "true");
    expect(section("deployment")).toHaveAttribute("data-open", "true");
    // A section at its default stays closed and says nothing.
    expect(section("queue")).toHaveAttribute("data-open", "false");
    expect(screen.queryByTestId("settings-section-queue-changed")).toBeNull();
    // Each section carries its former page's title and one-line description.
    expect(within(toggle("queue")).getByText("Queue")).toBeInTheDocument();
    expect(within(toggle("queue")).getByText("consumer backpressure and Redis retry cadence")).toBeInTheDocument();
  });

  it("Filter_HidesUnchangedSections_ButNeverAnUnsavedEdit", async () => {
    await loaded("runs");
    await screen.findByTestId("settings-section-limits-changed", {}, SLOW);
    fireEvent.click(toggle("queue"));
    fireEvent.change(await screen.findByTestId("setting-queue-parallel"), { target: { value: "9" } });

    fireEvent.click(screen.getByTestId("settings-only-changed"));
    expect(section("limits")).toBeVisible();
    expect(section("deployment")).toBeVisible();
    expect(section("queue")).toBeVisible(); // unsaved edit
    expect(section("orchestrator")).not.toBeVisible();
  });

  it("Filter_NothingChanged_SaysEveryValueIsItsDefault", async () => {
    await loaded("diagnostics");
    fireEvent.click(screen.getByTestId("settings-only-changed"));
    expect(screen.getByTestId("settings-none-changed")).toHaveTextContent("Every value here is its default.");
    expect(section("trace")).not.toBeVisible();
  });

  it("GroupSave_IsDirtyGated_AndPutsEachEditedDoc", async () => {
    await loaded("runs");
    expect(screen.getByTestId("settings-save")).toBeDisabled();
    fireEvent.click(toggle("orchestrator"));
    fireEvent.change(await screen.findByTestId("setting-orchestrator-walltime"), { target: { value: "3600" } });
    fireEvent.click(toggle("queue"));
    fireEvent.change(await screen.findByTestId("setting-queue-parallel"), { target: { value: "9" } });
    fireEvent.click(screen.getByTestId("settings-save"));
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(2));
    expect(saveSetting).toHaveBeenCalledWith("orchestrator", expect.objectContaining({ maxRunWallTimeSeconds: 3600 }));
    expect(saveSetting).toHaveBeenCalledWith("queue", expect.objectContaining({ maxParallelJobs: 9 }));
    await waitFor(() => expect(screen.getByTestId("settings-save")).toBeDisabled());
  });

  it("GroupSave_OneRefused_KeepsItsDraftAndError_TheOtherLands", async () => {
    saveSetting.mockImplementation((key: string, value: unknown) =>
      key === "queue" ? Promise.reject(new Error("maxParallelJobs must be at least 1")) : Promise.resolve(value));
    await loaded("runs");
    fireEvent.click(toggle("orchestrator"));
    fireEvent.change(await screen.findByTestId("setting-orchestrator-walltime"), { target: { value: "3600" } });
    fireEvent.click(toggle("queue"));
    fireEvent.change(await screen.findByTestId("setting-queue-parallel"), { target: { value: "0" } });
    fireEvent.click(screen.getByTestId("settings-save"));
    expect(await screen.findByTestId("settings-save-error-queue")).toHaveTextContent("maxParallelJobs must be at least 1");
    expect(screen.getByTestId("setting-queue-parallel")).toHaveValue(0);
    expect(saveSetting).toHaveBeenCalledWith("orchestrator", expect.objectContaining({ maxRunWallTimeSeconds: 3600 }));
    // The queue edit is still unsaved, so Save stays available for it.
    expect(screen.getByTestId("settings-save")).not.toBeDisabled();
    saveSetting.mockImplementation((_key: string, value: unknown) => Promise.resolve(value));
  });

  it("OpenParam_OpensThatSectionAndScrollsToIt", async () => {
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    await loaded("workflow", "holds");
    expect(section("holds")).toHaveAttribute("data-open", "true");
    expect(await screen.findByTestId("setting-holds-escalate")).toBeInTheDocument();
    await waitFor(() => expect(scrolled).toHaveBeenCalled());
    expect(section("dialogue")).toHaveAttribute("data-open", "false");
  });

  it("LoadRefusal_OpensTheSection_AndSaysWhy", async () => {
    fetchSetting.mockImplementation((key: string) =>
      key === "trace"
        ? Promise.reject(Object.assign(new Error("HTTP 403"), { status: 403 }))
        : Promise.resolve(SAVED[key]));
    await loaded("diagnostics");
    await waitFor(() => expect(section("trace")).toHaveAttribute("data-open", "true"), SLOW);
    expect(within(section("trace")).getByText(/403/)).toBeInTheDocument();
    expect(section("pipeline_data_flow")).toHaveAttribute("data-open", "false");
    fetchSetting.mockImplementation((key: string) => Promise.resolve(SAVED[key]));
  });

  it("NoDefault_FromTheServer_ShowsNoCount", async () => {
    fetchSettingDefault.mockImplementation(() => Promise.reject(new Error("HTTP 404")));
    await loaded("runs");
    expect(screen.queryByTestId("settings-section-limits-changed")).toBeNull();
    expect(section("limits")).toHaveAttribute("data-open", "false");
    fetchSettingDefault.mockImplementation((key: string) => Promise.resolve(DEFAULTS[key]));
  });

  // Every field a former page rendered renders in its section: the same form, the same test ids.
  it.each(SETTING_GROUPS.flatMap((g) => g.members.map((key) => [g.id, key] as const)))(
    "EveryMemberField_RendersInItsGroup %s/%s",
    async (groupId, key) => {
      render(<SettingsStudio settingKey={key} />);
      const form = await screen.findByTestId("settings-form");
      await waitFor(() => expect(within(form).queryByText("Loading…")).toBeNull());
      await waitFor(() => expect(form.querySelector("[data-testid^='setting-']")).not.toBeNull());
      const fields = [...form.querySelectorAll("[data-testid^='setting-']")].map((el) => el.getAttribute("data-testid"));
      cleanup();

      render(<SettingsGroupStudio groupId={groupId} open={key} />);
      const body = section(key);
      for (const id of fields) expect(await within(body).findByTestId(id!)).toBeInTheDocument();
    },
  );
});
