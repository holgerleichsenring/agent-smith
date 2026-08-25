import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SettingsStudio } from "../SettingsStudio";
import { resetCapabilitiesCache } from "../useCapabilities";

// p0353: the Settings pane — one typed form per global settings singleton. The
// forms load a doc, edit a local draft, and save through the same live-applying
// PUT the backend records + epoch-bumps. These tests drive the flat form
// (orchestrator), the enum select (skills), the nested cost-cap sub-forms, and the
// registries list — plus the dirty-gated Save.

const FIXTURES: Record<string, unknown> = {
  orchestrator: { registry: "ghcr.io/x", version: "1.0.0", maxRunWallTimeSeconds: 1800 },
  skills: { source: 0, version: "v3", path: null, url: null, sha256: null, cacheDir: "" },
  pipeline_cost_cap: {
    default: { usd: 5, tokens: 500000 },
    perPipeline: {},
    perTier: {
      Trivial: { usd: 1, tokens: 200000 },
      Small: { usd: 2, tokens: 400000 },
      Medium: { usd: 8, tokens: 1500000 },
      Large: { usd: 25, tokens: 5000000 },
    },
  },
  registries: [],
  sandbox: {
    agentRegistry: "ghcr.io/x",
    agentVersion: "0.48.0",
    stepTimeoutSeconds: 900,
    runCommandTimeoutSeconds: 300,
  },
  limits: {
    maxToolCallsPerSkill: 30,
    maxToolCallsPerInvestigator: 10,
    maxToolCallsPerVerifier: 20,
    maxLlmCallsPerSkill: 15,
    maxInputTokensPerSkillCall: 500000,
    maxOutputTokensPerSkillCall: 16000,
    maxSecondsPerSkillCall: 300,
    maxConcurrentSkillCalls: 10,
    maxSkillsPerPhase: 5,
    maxConcurrentSubAgents: 4,
    maxSubAgentsPerRun: 20,
  },
};

const saveSetting = vi.fn((_key: string, value: unknown) => Promise.resolve(value));

vi.mock("@/lib/configApi", () => ({
  fetchSetting: vi.fn((key: string) => Promise.resolve(FIXTURES[key])),
  saveSetting: (key: string, value: unknown) => saveSetting(key, value),
  fetchCapabilities: vi.fn().mockResolvedValue({
    trackerTypes: [],
    connectionTypes: [],
    agentProviders: ["claude", "openai"],
    resolutionStrategies: [],
    permissions: [],
    builtInRoles: [],
    pipelines: ["fix-bug", "feature"],
    roles: [],
  }),
}));

beforeEach(() => {
  vi.clearAllMocks();
  resetCapabilitiesCache();
});

describe("SettingsStudio", () => {
  it("FlatForm_LoadsValues_TitleAndSubtitle", async () => {
    render(<SettingsStudio settingKey="orchestrator" />);
    const walltime = await screen.findByTestId("setting-orchestrator-walltime");
    expect(walltime).toHaveValue(1800);
    expect(screen.getByTestId("setting-orchestrator-registry")).toHaveValue("ghcr.io/x");
    expect(screen.getByRole("heading", { name: /Orchestrator/ })).toBeInTheDocument();
  });

  it("Save_IsDirtyGated_ThenPersistsTheEditedDoc", async () => {
    render(<SettingsStudio settingKey="orchestrator" />);
    await screen.findByTestId("setting-orchestrator-walltime");
    // Untouched → Save disabled.
    expect(screen.getByTestId("settings-save")).toBeDisabled();
    // Edit a field → Save enables.
    fireEvent.change(screen.getByTestId("setting-orchestrator-walltime"), { target: { value: "3600" } });
    const save = screen.getByTestId("settings-save");
    expect(save).not.toBeDisabled();
    fireEvent.click(save);
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith(
      "orchestrator",
      expect.objectContaining({ maxRunWallTimeSeconds: 3600, registry: "ghcr.io/x" }),
    );
  });

  it("SkillsForm_SourceSelect_EditsTheEnumValue", async () => {
    render(<SettingsStudio settingKey="skills" />);
    const source = await screen.findByTestId("setting-skills-source");
    expect(source).toHaveValue("0");
    fireEvent.change(source, { target: { value: "1" } });
    fireEvent.click(screen.getByTestId("settings-save"));
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith("skills", expect.objectContaining({ source: 1 }));
  });

  it("CostCapForm_RendersDefaultPerTierAndPerPipelineSubForms", async () => {
    render(<SettingsStudio settingKey="pipeline_cost_cap" />);
    await screen.findByTestId("setting-costcap-default-usd");
    // Default + the four fixed tiers render.
    expect(screen.getByTestId("setting-costcap-default-usd")).toHaveValue(5);
    expect(screen.getByTestId("setting-costcap-tier-Large-usd")).toHaveValue(25);
    // A pipeline override can be added from the capabilities pipeline list (the
    // per-pipeline section starts collapsed — expand it first).
    fireEvent.click(screen.getByTestId("setting-costcap-pipelines-toggle"));
    fireEvent.change(screen.getByTestId("setting-costcap-pipeline-add"), { target: { value: "fix-bug" } });
    expect(await screen.findByTestId("setting-costcap-pipeline-fix-bug-usd")).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("settings-save"));
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith(
      "pipeline_cost_cap",
      expect.objectContaining({ perPipeline: expect.objectContaining({ "fix-bug": expect.anything() }) }),
    );
  });

  it("LimitsForm_GroupsFieldsIntoDomainSections", async () => {
    render(<SettingsStudio settingKey="limits" />);
    await screen.findByTestId("setting-limits-maxToolCallsPerSkill");
    // The 11 flat fields are grouped into three labelled sections.
    expect(screen.getByTestId("setting-limits-calls")).toBeInTheDocument();
    expect(screen.getByTestId("setting-limits-budgets")).toBeInTheDocument();
    expect(screen.getByTestId("setting-limits-subagents")).toBeInTheDocument();
    // Fields land under their group and load their values.
    expect(screen.getByTestId("setting-limits-maxToolCallsPerSkill")).toHaveValue(30);
    expect(screen.getByTestId("setting-limits-maxSubAgentsPerRun")).toHaveValue(20);
  });

  it("LimitsForm_ClearingARequiredNumber_RetainsTheLoadedValueNotZero", async () => {
    render(<SettingsStudio settingKey="limits" />);
    const field = await screen.findByTestId("setting-limits-maxToolCallsPerSkill");
    // Emptying the field keeps the loaded value rather than drafting a silent 0,
    // so nothing goes dirty and Save stays disabled.
    fireEvent.change(field, { target: { value: "" } });
    expect(field).toHaveValue(30);
    expect(screen.getByTestId("settings-save")).toBeDisabled();
  });

  // p0495: the two sandbox timeouts are one rule. A default above the cap is a number
  // the cap would never let apply, so the studio refuses it and names both values.
  it("SandboxSettings_RunCommandDefaultAboveTheCap_IsRefused_NamingBothValues", async () => {
    render(<SettingsStudio settingKey="sandbox" />);
    const runCommand = await screen.findByTestId("setting-sandbox-runcmd");
    // The section states the relationship before anything is typed.
    expect(screen.getByTestId("setting-sandbox-timeouts-note")).toHaveTextContent(
      /killed at the step cap/,
    );

    fireEvent.change(runCommand, { target: { value: "1200" } });

    const refusal = screen.getByTestId("settings-refusal");
    expect(refusal).toHaveTextContent("1200s");
    expect(refusal).toHaveTextContent("900s");
    expect(screen.getByTestId("settings-save")).toBeDisabled();
    fireEvent.click(screen.getByTestId("settings-save"));
    expect(saveSetting).not.toHaveBeenCalled();
  });

  it("SandboxSettings_RunCommandDefaultAtTheCap_Saves", async () => {
    render(<SettingsStudio settingKey="sandbox" />);
    const runCommand = await screen.findByTestId("setting-sandbox-runcmd");

    fireEvent.change(runCommand, { target: { value: "900" } });

    expect(screen.queryByTestId("settings-refusal")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("settings-save"));
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith(
      "sandbox",
      expect.objectContaining({ runCommandTimeoutSeconds: 900, stepTimeoutSeconds: 900 }),
    );
  });

  it("RegistriesForm_AddsAndEditsAFeed", async () => {
    render(<SettingsStudio settingKey="registries" />);
    await screen.findByTestId("setting-registry-add");
    fireEvent.click(screen.getByTestId("setting-registry-add"));
    const host = await screen.findByTestId("setting-registry-0-host");
    fireEvent.change(host, { target: { value: "pkgs.dev.azure.com" } });
    fireEvent.click(screen.getByTestId("settings-save"));
    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith("registries", [
      expect.objectContaining({ host: "pkgs.dev.azure.com" }),
    ]);
  });
});
