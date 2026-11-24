import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fetchConnectionRepos, type PocsSetting, type SettingValue, type StudioEntity } from "@/lib/configApi";
import {
  POC_ARCHIVE_DEFAULTS, POC_GITOPS_DEFAULTS, POC_LIFETIME_DEFAULTS, POC_ROLLOUT_DEFAULTS, POC_SCAN_DEFAULTS, POC_VARIABLES_DEFAULTS,
  PocsSettingsForm, newPocTarget, type PocsCatalog,
} from "../PocsSettingsForm";
import { SourceRepoPicker } from "../SourceRepoPicker";
import { placePocsProblems, pocsProblemSentences } from "../pocsProblems";

// 2026-11-24-f965: the PoC source repository is picked like a project's repos (catalog or a connection's discovered
// repos, one only), the form's text fields keep autofill off, the pull mode is a proper choice list, and what is
// missing or refused shows at its field.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConnectionRepos: vi.fn(),
}));

const mockedRepos = vi.mocked(fetchConnectionRepos);
const entity = (id: string) => ({ id }) as StudioEntity;

function Picker({ seen, start = "", repos = [entity("poc-source")] }: { seen: string[]; start?: string; repos?: StudioEntity[] }) {
  const [value, setValue] = useState(start);
  return (
    <SourceRepoPicker label="Source repository" help="one repo" value={value} repos={repos} connections={[entity("github")]}
      testId="src" onChange={(v) => { seen.push(v); setValue(v); }} />
  );
}

const OFF: PocsSetting = {
  enabled: false, projectAgent: null, projectTracker: null, targets: [], archive: POC_ARCHIVE_DEFAULTS,
  scan: POC_SCAN_DEFAULTS, blockedRetentionDays: 7, variables: POC_VARIABLES_DEFAULTS, gitops: POC_GITOPS_DEFAULTS,
  rollout: POC_ROLLOUT_DEFAULTS, runWallTimeSeconds: 2700, readinessPathMaxChars: 256, lifetime: POC_LIFETIME_DEFAULTS,
};
const CATALOG: PocsCatalog = {
  agents: [entity("agent-a")], trackers: [entity("tracker-a")], repos: [], connections: [entity("github")], secrets: [],
};

function Form({ start, saveError = null }: { start: PocsSetting; saveError?: string | null }) {
  const [value, setValue] = useState(start);
  return <PocsSettingsForm value={value} catalog={CATALOG} saveError={saveError}
    onChange={(next: SettingValue) => setValue(next as PocsSetting)} />;
}

beforeEach(() => {
  mockedRepos.mockReset();
  mockedRepos.mockResolvedValue({
    discoveredAt: "2026-11-24T09:00:00Z",
    repos: [{ name: "poc-sources", defaultBranch: "main" }, { name: "other", defaultBranch: null }],
  });
});

afterEach(() => vi.restoreAllMocks());

describe("SourceRepoPicker", () => {
  it("SourceRepoPicker_CatalogRepo_SetsItsId", () => {
    const seen: string[] = [];
    render(<Picker seen={seen} />);
    fireEvent.change(screen.getByTestId("src-catalog"), { target: { value: "poc-source" } });
    expect(seen).toEqual(["poc-source"]);
    expect(screen.getByTestId("src-selected")).toHaveTextContent("poc-source");
    expect(mockedRepos).not.toHaveBeenCalled();
  });

  it("SourceRepoPicker_ConnectionRow_SetsConnectionSlashName_OneAtATime", async () => {
    const seen: string[] = [];
    render(<Picker seen={seen} repos={[]} />);
    expect(screen.queryByTestId("src-catalog")).toBeNull();
    fireEvent.change(screen.getByTestId("src-connection"), { target: { value: "github" } });
    await screen.findByTestId("src-count");

    fireEvent.click(screen.getByTestId("src-discovered-poc-sources"));
    fireEvent.click(screen.getByTestId("src-discovered-other"));

    expect(seen).toEqual(["github/poc-sources", "github/other"]);
    expect(screen.getByTestId("src-discovered-other")).toBeChecked();
    expect(screen.getByTestId("src-discovered-poc-sources")).not.toBeChecked();
    expect(screen.getByTestId("src-discovered-other")).toHaveAttribute("type", "radio");
  });

  it("SourceRepoPicker_SavedConnectionValue_OpensOnItsConnection", async () => {
    render(<Picker seen={[]} start="github/poc-sources" />);
    expect(screen.getByTestId("src-connection")).toHaveValue("github");
    expect(await screen.findByTestId("src-discovered-poc-sources")).toBeChecked();
  });

  it("SourceRepoPicker_TypedName_IsOffered_AndAWildcardIsNot", async () => {
    mockedRepos.mockResolvedValue({ discoveredAt: null, repos: [] });
    const seen: string[] = [];
    render(<Picker seen={seen} repos={[]} />);
    fireEvent.change(screen.getByTestId("src-connection"), { target: { value: "github" } });
    await screen.findByTestId("src-undiscovered");

    fireEvent.change(screen.getByTestId("src-filter"), { target: { value: "poc-*" } });
    expect(screen.getByTestId("src-add")).toBeDisabled();
    expect(screen.getByTestId("src-no-wildcard")).toBeInTheDocument();

    fireEvent.change(screen.getByTestId("src-filter"), { target: { value: "typed-repo" } });
    fireEvent.click(screen.getByTestId("src-add"));
    expect(seen).toEqual(["github/typed-repo"]);
  });
});

describe("PocsSettingsForm autofill, choices and problems", () => {
  it("PocsSettingsForm_TextInputs_KeepAutofillOff_WithAFieldName", () => {
    render(<Form start={{ ...OFF, targets: [newPocTarget()] }} />);
    const name = screen.getByTestId("setting-pocs-target-0-name");
    expect(name).toHaveAttribute("autocomplete", "off");
    expect(name).toHaveAttribute("name", "setting-pocs-target-0-name");
    expect(name).toHaveAttribute("data-1p-ignore", "true");
    expect(name).toHaveAttribute("data-lpignore", "true");
    for (const input of document.querySelectorAll('input[type="text"]'))
      expect(input).toHaveAttribute("autocomplete", "off");
  });

  it("PocsSettingsForm_PullMode_IsAChoiceList_RadioThenTitleAndHint", () => {
    render(<Form start={{ ...OFF, targets: [newPocTarget()] }} />);
    const group = within(screen.getByTestId("setting-pocs-target-0-registry-pull-mode")).getByRole("radiogroup");
    expect(group).toHaveClass("poc-choices");
    const choice = screen.getByTestId("setting-pocs-target-0-registry-pull-mode-pull_secret-choice");
    expect(choice).toHaveClass("poc-choice", "on");
    expect(choice.firstElementChild).toHaveAttribute("type", "radio");
    expect(choice.querySelector("b")).not.toBeNull();
    expect(choice.querySelector(".poc-meta")).not.toBeNull();
    expect(screen.getByTestId("setting-pocs-target-0-registry-pull-mode-cluster_pulls-choice")).not.toHaveClass("on");
  });

  it("PocsSettingsForm_Enabled_SaysRequiredAtEveryEmptyRequiredField_BeforeSaving", () => {
    render(<Form start={OFF} />);
    expect(screen.queryByTestId("setting-pocs-agent-required")).toBeNull();

    fireEvent.click(screen.getByTestId("setting-pocs-enabled"));
    expect(screen.getByTestId("setting-pocs-agent-required")).toBeInTheDocument();
    expect(screen.getByTestId("setting-pocs-tracker-required")).toBeInTheDocument();
    expect(screen.getByTestId("setting-pocs-targets-required")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("setting-pocs-target-add"));
    expect(screen.queryByTestId("setting-pocs-targets-required")).toBeNull();
    expect(screen.getByTestId("setting-pocs-target-0-name-required")).toBeInTheDocument();
    expect(screen.getByTestId("setting-pocs-target-0-source-required")).toBeInTheDocument();

    fireEvent.change(screen.getByTestId("setting-pocs-agent"), { target: { value: "agent-a" } });
    expect(screen.queryByTestId("setting-pocs-agent-required")).toBeNull();
  });

  it("PocsSettingsForm_RefusedSave_PlacesEachSentenceAtItsField", () => {
    const main = { ...newPocTarget(), name: "main" };
    const message = "Invalid 'pocs' settings: pocs.project_agent is empty while pocs.enabled is true; name an agent, "
      + "because every poc- project needs one to resolve; pocs.targets[].source_repo of target 'main' is empty; name the "
      + "repository PoC sources are committed to; pocs.targets[].registry.login_server is empty (target 'main'); "
      + "pocs.archive.max_zip_mb is 0; it must be 1 or more";
    render(<Form start={{ ...OFF, enabled: true, targets: [main] }} saveError={message} />);

    expect(screen.getByTestId("setting-pocs-agent-problem")).toHaveTextContent(/^pocs\.project_agent is empty/);
    expect(screen.getByTestId("setting-pocs-target-0-source-problem")).toHaveTextContent(
      "pocs.targets[].source_repo of target 'main' is empty; name the repository PoC sources are committed to");
    // A target field without a place of its own is shown at the top of its target.
    expect(screen.getByTestId("setting-pocs-target-0-problem")).toHaveTextContent("registry.login_server");
    // The sentence replaces the plain "required" hint.
    expect(screen.queryByTestId("setting-pocs-agent-required")).toBeNull();
  });
});

describe("pocsProblems", () => {
  it("PocsProblemSentences_SplitsOnlyWhereTheNextKeyStarts", () => {
    expect(pocsProblemSentences("Invalid 'pocs' settings: pocs.targets is empty; name one; pocs.project_tracker is empty"))
      .toEqual(["pocs.targets is empty; name one", "pocs.project_tracker is empty"]);
    expect(pocsProblemSentences("HTTP 500")).toEqual([]);
  });

  it("PlacePocsProblems_NameSentences_FindTheirTargetByTheQuotedName", () => {
    const placed = placePocsProblems(
      "Invalid 'pocs' settings: pocs.targets[].name is 'Main'; it must be a DNS label", ["other", "Main"]);
    expect(placed.target[1].name).toEqual(["pocs.targets[].name is 'Main'; it must be a DNS label"]);
    expect(placed.target[0]).toBeUndefined();
  });
});
