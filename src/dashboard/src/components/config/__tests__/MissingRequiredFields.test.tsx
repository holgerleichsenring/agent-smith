import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";
import type { ConfigFinding } from "@/lib/configApi";

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
        defaultPipeline: "fix-bug",
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
            // 2026-09-16-a4d7: declared, and OPTIONAL — required would make every tracker
            // configured before that phase unsaveable.
            { key: "defaultPipeline", label: "default pipeline", required: false, kind: "text" },
          ],
        },
      ],
      connectionTypes: [],
      agentProviders: ["anthropic"],
      resolutionStrategies: ["tag"],
      permissions: [],
      builtInRoles: [],
      // The OFFERABLE set (PipelinePresets.Names) — retired aliases are absent by design.
      pipelines: ["code", "security-scan"],
      roles: [{ key: "coding", optional: false }],
    }),
    // 2026-09-14-620e: the template form reads context names live; a wholesale
    // module mock has to declare it or the form throws on mount.
    fetchProjectContexts: vi.fn().mockResolvedValue({ contexts: [], unreadableReason: null }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

// The module mock's factory is hoisted and cannot be referenced from a test, so the one
// finding it serves is restated here for the tests that override the validator and put it
// back. Two copies of a literal, in one file, is cheaper than a shared mutable fixture.
const parkFindingForRestore: ConfigFinding = {
  subsystem: "configuration",
  severity: "blocking",
  reason:
    "Project 'demo' github_trigger: pipeline 'code' can park a run on an operator question, " +
    "but needs_clarification_status is not set.",
  project: "demo",
  trigger: "github_trigger",
  field: "needs_clarification_status",
};

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

    // 2026-09-16-74a2: three pipeline fields became one, and it is the DEFAULT — the
    // legacy singular left the form (the loader shim still appends it to the list).
    fireEvent.click(await screen.findByTestId("form-tab-pipeline"));
    const pipeline = (await screen.findByTestId("form-field-defaultPipeline")) as HTMLSelectElement;
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

    fireEvent.click(await screen.findByTestId("form-tab-pipeline"));
    const pipeline = (await screen.findByTestId("form-field-defaultPipeline")) as HTMLSelectElement;
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
    // p0499: a map is edited as rows of key/value inputs, not as `key: value` text —
    // the operator's label keys contain colons, and the text form shredded them.
    expect(screen.getByTestId("form-field-pipelineFromLabel-add")).toBeInTheDocument();
    expect(screen.queryByTestId("form-field-pipelineFromLabel")?.tagName).not.toBe("TEXTAREA");
  });

  it("Drawer_ProjectKind_CarriesTheWideModifier", async () => {
    // 2026-09-16-74a2: one drawer element serves all seven kinds, so the project's width
    // is a MODIFIER on it. jsdom loads no stylesheet, so the 560 itself cannot be
    // asserted — what CAN be pinned is that the modifier is applied for a project and not
    // for a secret, which is one field and asks for no room.
    const { unmount } = render(
      <ConfigCatalogProvider>
        <ConfigStudio section="projects" />
      </ConfigCatalogProvider>,
    );
    fireEvent.click(await screen.findByTestId("config-card-edit-legacy"));
    expect(screen.getByLabelText("Create or edit").className).toContain("wide-project");
    unmount();

    render(
      <ConfigCatalogProvider>
        <ConfigStudio section="secrets" />
      </ConfigCatalogProvider>,
    );
    fireEvent.click(await screen.findByTestId("config-new-secrets"));
    expect(screen.getByLabelText("Create or edit").className).not.toContain("wide-project");
  });

  it("ProjectForm_UnfinishedTemplate_BlocksSaveAndNamesIt", async () => {
    // A binding missing its context, project, repo or template context is refused where
    // templates are fetched. Letting the save through stores a declaration that can never
    // resolve, and the operator learns it on the next run instead of on the form.
    const api = await import("@/lib/configApi");
    vi.mocked(api.validateProjectDraft).mockResolvedValue([]);
    try {
      await openProject("legacy");
      await waitFor(() => expect(screen.getByTestId("config-drawer-save")).not.toBeDisabled());

      fireEvent.click(screen.getByTestId("form-tab-templates"));
      fireEvent.click(screen.getByTestId("form-templates-add"));

      expect(screen.getByTestId("config-drawer-save")).toBeDisabled();
      expect(screen.getByTestId("config-drawer-blocked").textContent).toContain(
        "template 1 is unfinished",
      );
      // And the section that needs attention is marked on its own tab.
      expect(screen.getByTestId("form-tab-templates")).toHaveAttribute("data-marked", "true");
    } finally {
      vi.mocked(api.validateProjectDraft).mockResolvedValue([parkFindingForRestore]);
    }
  });

  it("TrackerForm_MissingDefaultPipeline_IsAdvisoryAndNamesTheField", async () => {
    // 2026-09-16-a4d7: a tracker declaring neither a label map nor a fallback routes every
    // ticket to the hardcoded preset. The server says so; the form shows it ON the field it
    // is about — the tracker findings all said "type" before — and does NOT block the save,
    // because every tracker configured before that phase is in exactly this state.
    const api = await import("@/lib/configApi");
    vi.mocked(api.validateTrackerDraft).mockResolvedValue([
      {
        subsystem: "configuration",
        severity: "advisory",
        reason:
          "Tracker 'gh' declares no pipeline_from_label and no default pipeline: " +
          "every ticket it routes runs 'fix-bug'.",
        project: null,
        trigger: null,
        field: "defaultPipeline",
      },
    ]);
    try {
      render(
        <ConfigCatalogProvider>
          <ConfigStudio section="trackers" />
        </ConfigCatalogProvider>,
      );
      fireEvent.click(await screen.findByTestId("config-card-edit-gh"));

      expect(await screen.findByTestId("form-field-defaultPipeline")).toBeInTheDocument();
      const slot = await screen.findByTestId("form-finding-defaultPipeline");
      expect(slot).toHaveAttribute("data-severity", "advisory");
      expect(slot.textContent).toContain("fix-bug");
      // Advisory never disables Save.
      await waitFor(() => expect(screen.getByTestId("config-drawer-save")).not.toBeDisabled());
    } finally {
      vi.mocked(api.validateTrackerDraft).mockResolvedValue([]);
    }
  });
});
