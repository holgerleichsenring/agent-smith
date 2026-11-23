import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { PocsSetting, SettingValue, StudioEntity } from "@/lib/configApi";
import { POC_ARCHIVE_DEFAULTS, POC_SCAN_DEFAULTS, PocsSettingsForm, type PocsCatalog } from "../PocsSettingsForm";

// 2026-11-23-933da: the PoCs settings form edits the switch, the poc- projects' agent and tracker, and the targets,
// and what it hands back is the wire shape the settings route saves.

const entity = (id: string) => ({ id }) as StudioEntity;
const CATALOG: PocsCatalog = {
  agents: [entity("claude")], trackers: [entity("jira")], repos: [entity("poc-source")], connections: [entity("github")],
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
    expect(saved.scan).toEqual({ osvImage: image, timeoutSeconds: 600, unfixedCriticalBlock: false, secretMinConfidence: 9 });
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
});
