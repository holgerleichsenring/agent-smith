import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { PocsSetting, SettingValue, StudioEntity } from "@/lib/configApi";
import { PocsSettingsForm, type PocsCatalog } from "../PocsSettingsForm";

// 2026-11-23-933da: the PoCs settings form edits the switch, the poc- projects' agent and tracker, and the targets,
// and what it hands back is the wire shape the settings route saves.

const entity = (id: string) => ({ id }) as StudioEntity;
const CATALOG: PocsCatalog = {
  agents: [entity("claude")], trackers: [entity("jira")], repos: [entity("poc-source")], connections: [entity("github")],
};
const OFF: PocsSetting = { enabled: false, projectAgent: null, projectTracker: null, targets: [] };

function Harness({ seen }: { seen: SettingValue[] }) {
  const [value, setValue] = useState<PocsSetting>(OFF);
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
});
