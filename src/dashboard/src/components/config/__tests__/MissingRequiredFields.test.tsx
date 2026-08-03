import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";

// p0392: the studio says what is missing BEFORE the save. On 2026-07-31 a trigger was
// missing needs_clarification_status, the server refused to start, and the way out was a
// rollback, a CLI export, a hand edit, an import and a roll-forward — because the field
// could not be set in the UI at all. Two halves: the field is now offered (from the
// capabilities descriptor, not a hand-written list), and the SERVER's own rules are asked
// about the draft, so the studio never restates a requirement of its own.

vi.mock("@/lib/configApi", () => {
  const parkFinding = {
    subsystem: "configuration",
    severity: "blocking",
    reason:
      "Project 'demo' github_trigger: pipeline 'code' can park a run on an operator question, " +
      "but needs_clarification_status is not set.",
    project: "demo",
    trigger: "github_trigger",
    field: "needs_clarification_status",
  };
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    update: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    remove: vi.fn().mockResolvedValue(undefined),
  });
  return {
    agentsApi: client([{ id: "claude" }]),
    trackersApi: client([
      {
        id: "gh",
        type: "github",
        authSecret: "PAT",
        url: "https://github.com/acme",
      },
    ]),
    connectionsApi: client([]),
    reposApi: client([{ id: "repo", name: "https://github.com/acme/repo", branch: "main" }]),
    projectsApi: client([
      {
        id: "legacy",
        agent: "claude",
        tracker: "gh",
        repos: ["repo"],
        // p0393: a RETIRED preset name. It still runs, so it must still load.
        pipeline: "fix-bug",
        pipelines: ["fix-bug"],
        resolution: { strategy: "tag", value: "legacy" },
      },
    ]),
    mcpServersApi: client([]),
    secretsApi: client([{ id: "PAT" }]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    validateProjectDraft: vi.fn().mockResolvedValue([parkFinding]),
    validateTrackerDraft: vi.fn().mockResolvedValue([]),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [
        {
          type: "github",
          fields: [
            { key: "url", label: "repository url", required: true, kind: "text" },
            { key: "authSecret", label: "auth secret", required: true, kind: "text" },
            {
              key: "needsClarificationStatus",
              label: "needs-clarification status",
              required: false,
              kind: "text",
            },
            { key: "zeroMatchComment", label: "comment when nothing matched", required: false, kind: "bool" },
            { key: "pipelineFromLabel", label: "pipeline by label", required: false, kind: "map" },
          ],
        },
      ],
      connectionTypes: [],
      agentProviders: ["anthropic"],
      resolutionStrategies: ["tag"],
      // The OFFERABLE set (PipelinePresets.Names) — retired aliases are absent by design.
      pipelines: ["code", "security-scan"],
      roles: [{ key: "coding", optional: false }],
    }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

beforeEach(() => vi.clearAllMocks());

async function openProject(id: string) {
  render(
    <ConfigCatalogProvider>
      <ConfigStudio section="projects" />
    </ConfigCatalogProvider>,
  );
  fireEvent.click(await screen.findByTestId(`config-card-edit-${id}`));
}

describe("Config Studio shows what is missing (p0392)", () => {
  it("Studio_TriggerMissingNeedsClarificationStatus_IsFlaggedBeforeSave", async () => {
    await openProject("legacy");

    // The rule ran on the SERVER; the studio renders what it was told, naming the field.
    const finding = await screen.findByTestId("form-draft-finding-needs_clarification_status");
    expect(finding.textContent).toContain("needs_clarification_status");
    expect(finding).toHaveAttribute("data-severity", "blocking");
  });

  it("Studio_RequiredFieldEmpty_BlocksSaveAndNamesTheField", async () => {
    await openProject("legacy");

    await screen.findByTestId("form-draft-finding-needs_clarification_status");
    await waitFor(() => expect(screen.getByTestId("config-drawer-save")).toBeDisabled());
    expect(screen.getByTestId("config-drawer-blocked").textContent).toContain(
      "needs_clarification_status",
    );
  });

  it("Studio_StoredRetiredPipelineName_LoadsAndIsLabelledRetired", async () => {
    await openProject("legacy");

    const pipeline = (await screen.findByTestId("form-field-pipeline")) as HTMLSelectElement;
    // It LOADS: the stored value survives opening the form.
    expect(pipeline.value).toBe("fix-bug");
    expect(pipeline.querySelector('option[value="fix-bug"]')).not.toBeNull();
    // And it is named for what it is, rather than silently rewritten.
    expect(pipeline.closest(".field")?.textContent).toContain("retired");
  });

  it("Studio_PipelinePicker_OffersOnlyCurrentNames", async () => {
    render(
      <ConfigCatalogProvider>
        <ConfigStudio section="projects" />
      </ConfigCatalogProvider>,
    );
    fireEvent.click(await screen.findByTestId("config-new-projects"));

    const pipeline = (await screen.findByTestId("form-field-pipeline")) as HTMLSelectElement;
    const offered = [...pipeline.querySelectorAll("option")]
      .map((o) => o.getAttribute("value"))
      .filter((v) => v !== "");
    expect(offered).toEqual(["code", "security-scan"]);
    expect(offered).not.toContain("fix-bug");
  });

  it("Studio_TrackerForm_OffersTheParkStatusAndEveryDeclaredShape", async () => {
    render(
      <ConfigCatalogProvider>
        <ConfigStudio section="trackers" />
      </ConfigCatalogProvider>,
    );
    fireEvent.click(await screen.findByTestId("config-card-edit-gh"));

    // The field the outage was about is editable at all — it was not, before p0392.
    expect(await screen.findByTestId("form-field-needsClarificationStatus")).toBeInTheDocument();
    // And the shapes come from the descriptor, so a map/bool field needs no UI change.
    expect(screen.getByTestId("form-field-zeroMatchComment")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-pipelineFromLabel").tagName).toBe("TEXTAREA");
  });
});
