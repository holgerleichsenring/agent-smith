import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PocsSetting, SettingValue, StudioEntity } from "@/lib/configApi";
import * as configApi from "@/lib/configApi";
import * as pocsApi from "@/lib/pocsApi";
import { POC_ARCHIVE_DEFAULTS, POC_SCAN_DEFAULTS, PocsSettingsForm, type PocsCatalog } from "../PocsSettingsForm";
import { POC_BUILD_DEFAULTS } from "../PocBuildFields";

// 2026-11-23-933da: the PoCs settings form edits the switch, the poc- projects' agent and tracker, and the targets,
// and what it hands back is the wire shape the settings route saves.

const entity = (id: string) => ({ id }) as StudioEntity;
const CATALOG: PocsCatalog = {
  agents: [entity("claude")], trackers: [entity("jira")], repos: [entity("poc-source")], connections: [entity("github")],
  secrets: [entity("poc_pull_token")],
};
const OFF: PocsSetting = {
  enabled: false, projectAgent: null, projectTracker: null, targets: [], archive: POC_ARCHIVE_DEFAULTS,
  scan: POC_SCAN_DEFAULTS, blockedRetentionDays: 7,
};

function Harness({ seen, start = OFF }: { seen: SettingValue[]; start?: PocsSetting }) {
  const [value, setValue] = useState<PocsSetting>(start);
  return (
    <PocsSettingsForm value={value} catalog={CATALOG}
      onChange={(next) => {
        seen.push(next);
        setValue(next as PocsSetting);
      }} />
  );
}

describe("PocsSettingsForm", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("PocsSettingsForm_SwitchAndATarget_RoundTripToTheWireShape", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));
    fireEvent.change(screen.getByTestId("setting-pocs-agent"), { target: { value: "claude" } });
    fireEvent.change(screen.getByTestId("setting-pocs-tracker"), { target: { value: "jira" } });
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-name"), { target: { value: "main" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-source"), { target: { value: "github/poc-source" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-domain"), { target: { value: "{app}.poc.example.internal" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-reminders"), { target: { value: "10, 2" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved).toEqual({
      enabled: true, projectAgent: "claude", projectTracker: "jira",
      targets: [{
        name: "main", sourceRepo: "github/poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
        domainPattern: "{app}.poc.example.internal", lifetimeDays: 90, reminderDays: [10, 2], perPersonLimit: 3,
        registry: {
          type: "acr", loginServer: "", repository: "pocs", pullUser: "", pullAuth: "", pullMode: "pull_secret",
        },
        build: POC_BUILD_DEFAULTS,
      }],
      archive: POC_ARCHIVE_DEFAULTS,
      scan: POC_SCAN_DEFAULTS,
      blockedRetentionDays: 7,
    });
  });

  it("PocsSettingsForm_NewTarget_HasNoAddressDefault_OnlyAPlaceholder_AndCanBeRemoved", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));

    expect(screen.getByTestId("setting-pocs-target-0-domain")).toHaveValue("");
    expect(screen.getByTestId("setting-pocs-target-0-domain")).toHaveAttribute("placeholder", "{app}.poc.example.internal");

    fireEvent.click(screen.getByTestId("setting-pocs-target-0-remove"));
    expect((seen[seen.length - 1] as PocsSetting).targets).toEqual([]);
    expect(screen.getByTestId("setting-pocs-no-target")).toBeInTheDocument();
  });

  // 2026-11-23-933db: the archive bounds are ten number fields; each edit round-trips, and a value read without archive
  // is given the defaults and sends them back, because a save without archive would reset the limits.
  it("PocsSettingsForm_ArchiveFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-archive-toggle"));

    expect(screen.getByTestId("setting-pocs-archive-maxZipMb")).toHaveValue(25);
    expect(screen.getByTestId("setting-pocs-archive-mismatchFileLimit")).toHaveValue(100);
    fireEvent.change(screen.getByTestId("setting-pocs-archive-maxZipMb"), { target: { value: "40" } });
    fireEvent.change(screen.getByTestId("setting-pocs-archive-mismatchRatioPercent"), { target: { value: "70" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.archive).toEqual({ ...POC_ARCHIVE_DEFAULTS, maxZipMb: 40, mismatchRatioPercent: 70 });
  });

  it("PocsSettingsForm_ValueWithoutArchive_SendsTheDefaultsBack", () => {
    const seen: SettingValue[] = [];
    const legacy = { enabled: false, projectAgent: null, projectTracker: null, targets: [] } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));

    expect((seen[seen.length - 1] as PocsSetting).archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  // 2026-11-23-933dc: the scan fields and the blocked-upload retention round-trip; the secret line is fixed text; a value
  // read without scan or retention gets the defaults and sends them back, beside archive.
  it("PocsSettingsForm_ScanFields_EditAndRoundTrip_WithTheFixedSecretLine", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-scan-toggle"));

    expect(screen.getByTestId("setting-pocs-scan-secret-fixed")).toHaveTextContent("A blocking secret cannot be overruled.");
    expect(screen.getByTestId("setting-pocs-scan-osvImage")).toHaveValue("");
    expect(screen.getByTestId("setting-pocs-scan-timeoutSeconds")).toHaveValue(300);
    expect(screen.getByTestId("setting-pocs-scan-secretMinConfidence")).toHaveValue(8);
    expect(screen.getByTestId("setting-pocs-blockedRetentionDays")).toHaveValue(7);

    const image = "ghcr.io/google/osv-scanner@sha256:" + "a".repeat(64);
    fireEvent.change(screen.getByTestId("setting-pocs-scan-osvImage"), { target: { value: image } });
    fireEvent.change(screen.getByTestId("setting-pocs-scan-timeoutSeconds"), { target: { value: "600" } });
    fireEvent.click(screen.getByTestId("setting-pocs-scan-unfixedCriticalBlock"));
    fireEvent.change(screen.getByTestId("setting-pocs-scan-secretMinConfidence"), { target: { value: "9" } });
    fireEvent.change(screen.getByTestId("setting-pocs-blockedRetentionDays"), { target: { value: "14" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.scan).toEqual({ osvImage: image, timeoutSeconds: 600, unfixedCriticalBlock: false, secretMinConfidence: 9, trivyImage: "" });
    expect(saved.blockedRetentionDays).toBe(14);
    expect(saved.archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  it("PocsSettingsForm_ValueWithoutScan_SendsScanAndRetentionDefaultsBack", () => {
    const seen: SettingValue[] = [];
    const legacy = {
      enabled: false, projectAgent: null, projectTracker: null, targets: [], archive: POC_ARCHIVE_DEFAULTS,
    } as unknown as PocsSetting;
    render(<Harness seen={seen} start={legacy} />);

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));

    const saved = seen[seen.length - 1] as PocsSetting;
    expect(saved.scan).toEqual(POC_SCAN_DEFAULTS);
    expect(saved.blockedRetentionDays).toBe(7);
    expect(saved.archive).toEqual(POC_ARCHIVE_DEFAULTS);
  });

  // 2026-11-23-933de: the registry section saves the pull token's secret id and the pull mode, and Test connection checks
  // the values in the form, showing the server's steps.
  it("PocsSettingsForm_Registry_SavesPullTokenAndMode_AndChecksTheFormValues", async () => {
    const check = vi.spyOn(configApi, "checkPocRegistryDraft").mockResolvedValue({
      ok: true,
      steps: [
        { key: "secret", label: "Secret", ok: true, detail: "Secret 'poc_pull_token' has a value." },
        { key: "scope", label: "Scope", ok: true, detail: "The pull token reaches pocregistry.azurecr.io/pocs." },
      ],
    });
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    const id = "setting-pocs-target-0-registry";

    fireEvent.change(screen.getByTestId(`${id}-login-server`), { target: { value: "pocregistry.azurecr.io" } });
    fireEvent.change(screen.getByTestId(`${id}-pull-user`), { target: { value: "poc-pull" } });
    fireEvent.change(screen.getByTestId(`${id}-pull-auth`), { target: { value: "poc_pull_token" } });
    fireEvent.click(screen.getByTestId(`${id}-pull-mode-cluster_pulls`));

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    const registry = {
      type: "acr", loginServer: "pocregistry.azurecr.io", repository: "pocs", pullUser: "poc-pull",
      pullAuth: "poc_pull_token", pullMode: "cluster_pulls",
    };
    expect(saved.targets[0].registry).toEqual(registry);

    fireEvent.click(screen.getByTestId(`${id}-check-run`));
    expect(check).toHaveBeenCalledWith(registry, expect.anything());
    expect(await screen.findByTestId(`${id}-check-step-scope`)).toHaveTextContent(
      "The pull token reaches pocregistry.azurecr.io/pocs.");
  });

  // 2026-11-23-933dd: the Trivy image, the build block of a target and the Install action.
  it("PocsSettingsForm_TrivyImageAndBuildFields_EditAndRoundTrip", () => {
    const seen: SettingValue[] = [];
    render(<Harness seen={seen} />);
    fireEvent.click(screen.getByTestId("setting-pocs-scan-toggle"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));

    const trivy = "aquasec/trivy@sha256:" + "b".repeat(64);
    fireEvent.change(screen.getByTestId("setting-pocs-scan-trivyImage"), { target: { value: trivy } });
    expect(screen.getByTestId("setting-pocs-target-0-build-buildTimeoutMinutes")).toHaveValue(12);
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-build-buildTimeoutMinutes"), { target: { value: "15" } });
    fireEvent.change(screen.getByTestId("setting-pocs-target-0-build-githubEnvironment"), { target: { value: "registry-push" } });

    const saved = JSON.parse(JSON.stringify(seen[seen.length - 1])) as PocsSetting;
    expect(saved.scan.trivyImage).toBe(trivy);
    expect(saved.targets[0].build).toEqual({ ...POC_BUILD_DEFAULTS, buildTimeoutMinutes: 15, githubEnvironment: "registry-push" });
  });

  it("PocsSettingsForm_Install_IsOffWhileThePocsSwitchIs_AndSaysTheRunsOutcome", async () => {
    const install = vi.spyOn(pocsApi, "installPocTarget").mockResolvedValue({
      target: "main", succeeded: true, runId: "r1", message: "The build pipeline of target 'main' is commit abc123.",
    });
    const target = { name: "main", sourceRepo: "poc-source", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
      domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3, registry: null };
    const { unmount } = render(<Harness seen={[]} start={{ ...OFF, targets: [target] }} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));
    expect(screen.getByTestId("setting-pocs-target-0-build-install")).toBeDisabled();
    expect(screen.getByTestId("setting-pocs-target-0-build-install-said")).toHaveTextContent("Turn PoCs on to install.");
    unmount();

    render(<Harness seen={[]} start={{ ...OFF, enabled: true, targets: [target] }} />);
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-toggle"));
    fireEvent.click(screen.getByTestId("setting-pocs-target-0-build-install"));

    expect(install).toHaveBeenCalledWith("main");
    expect(await screen.findByText("The build pipeline of target 'main' is commit abc123.")).toBeInTheDocument();
  });
});
