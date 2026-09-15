import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { EntityForm } from "../EntityForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { ConfigCapabilities, StudioProject } from "@/lib/configApi";
import { fetchConnectionRepos, fetchProjectContexts } from "@/lib/configApi";

// p0345c: the repo picker talks to the discovery cache — mock only that call,
// the rest of the module (types, entities' CRUD clients) stays real.
// 2026-09-14-620e: the template form reads contexts through a second call; it is
// mocked here too, because every project-form render now makes it.
vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConnectionRepos: vi.fn(),
  fetchProjectContexts: vi.fn(),
}));

const mockedRepos = vi.mocked(fetchConnectionRepos);
const mockedContexts = vi.mocked(fetchProjectContexts);

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: { model: "c" }, scan: { model: "s" } }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", organization: "o", project: "p", authSecret: "T" }],
  connections: [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "T", defaultBranch: "main" },
  ],
  repos: [
    { id: "web", name: "web", branch: "main" },
    { id: "api", name: "api", branch: "main" },
  ],
  projects: [
    // 2026-09-14-620e: the template target. ProjectTemplateRules refuses a repo the
    // named project does not carry, so the form offers THIS project's refs.
    {
      id: "refapp",
      agent: "gpt5",
      tracker: "azdo",
      repos: ["api"],
      pipeline: "",
      pipelines: [],
      resolution: null,
    },
  ],
  "mcp-servers": [],
  secrets: [{ id: "K" }, { id: "T" }],
};

const capabilities: ConfigCapabilities = {
  trackerTypes: [],
  connectionTypes: [],
  agentProviders: ["azure-openai"],
  resolutionStrategies: ["tag", "area_path", "repo", "to_address"],
  permissions: [],
  builtInRoles: [],
  pipelines: ["feature-implementation", "api-scan"],
  roles: [],
};

function Harness({ initial }: { initial?: Partial<StudioProject> } = {}) {
  const [draft, setDraft] = useState<StudioProject>({
    id: "proj",
    agent: "",
    tracker: "",
    repos: [],
    pipeline: "",
    pipelines: [],
    resolution: null,
    ...initial,
  });
  return (
    <EntityForm
      kind="projects"
      draft={draft}
      onChange={(n) => setDraft(n as StudioProject)}
      catalog={catalog}
      capabilities={capabilities}
      isNew
    />
  );
}

beforeEach(() => {
  mockedContexts.mockReset();
  mockedContexts.mockResolvedValue({ contexts: ["server", "client"], unreadableReason: null });
  mockedRepos.mockReset();
  mockedRepos.mockResolvedValue({
    discoveredAt: "2026-07-17T09:00:00Z",
    repos: [
      { name: "Sample.Api", defaultBranch: "main" },
      { name: "Sample.Web", defaultBranch: null },
    ],
  });
});

describe("ProjectForm", () => {
  it("ProjectForm_RefsPickedFromCatalog_NeverFreeText", () => {
    render(<Harness />);
    // agent + tracker refs are <select>s (pick-only), never text inputs.
    const agent = screen.getByTestId("form-ref-agent");
    const tracker = screen.getByTestId("form-ref-tracker");
    expect(agent.tagName).toBe("SELECT");
    expect(tracker.tagName).toBe("SELECT");
    // The options come straight from the catalog.
    expect(agent.querySelector('option[value="gpt5"]')).not.toBeNull();
    expect(tracker.querySelector('option[value="azdo"]')).not.toBeNull();
    // Repos are pick-only toggle chips, one per catalog repo — no text entry.
    expect(screen.getByTestId("form-ref-repos-option-web")).toBeInTheDocument();
    expect(screen.getByTestId("form-ref-repos-option-api")).toBeInTheDocument();
  });

  it("ProjectForm_IntegrityFlipsGreen_WhenAllRefsResolve", () => {
    render(<Harness />);
    // Starts unresolved (no refs picked yet).
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    // Still amber until at least one repo is chosen.
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));

    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    // p0343c: the mock's integrity copy
    expect(screen.getByTestId("project-integrity")).toHaveTextContent("Every reference resolves");
  });

  it("RepoPicker_OffersDiscoveredRepos_AndWildcard", async () => {
    // p0345c: picking a connection loads its discovery cache — the repos that
    // ACTUALLY exist there are offered for selection, and a wildcard/glob stays
    // possible. p0488: they are filterable rows, and the wildcard comes from
    // the filter box itself.
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });

    // The discovered repos render as selectable rows.
    const api = await screen.findByTestId("form-connref-discovered-Sample.Api");
    expect(screen.getByTestId("form-connref-discovered-Sample.Web")).toBeInTheDocument();
    expect(mockedRepos).toHaveBeenCalledWith("conn", expect.anything());

    // Toggling a discovered repo adds the conn-scoped ref chip…
    fireEvent.click(api);
    expect(api).toHaveAttribute("data-selected", "true");
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    // …and toggling again removes it.
    fireEvent.click(screen.getByTestId("form-connref-discovered-Sample.Api"));
    expect(screen.queryByTestId("form-connref-chip-conn/Sample.Api")).toBeNull();

    // The wildcard path stays: a glob is typed into the filter box, not picked.
    fireEvent.change(screen.getByTestId("form-connref-filter"), { target: { value: "*" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));
    expect(screen.getByTestId("form-connref-chip-conn/*")).toBeInTheDocument();
    expect(screen.getByTestId("wiring-repo-conn/*")).toHaveAttribute("data-resolved", "true");
  });

  it("RepoPicker_NotDiscoveredYet_HonestState_FreeTextStillWorks", async () => {
    // discoveredAt null = the discovery never ran — the picker says so and
    // falls back to typing a name (p0488: into the filter box, the only box)
    // instead of pretending an empty inventory.
    mockedRepos.mockResolvedValue({ discoveredAt: null, repos: [] });
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });

    const honest = await screen.findByTestId("form-connref-undiscovered");
    expect(honest).toHaveTextContent("not discovered yet — run a discovery or type a name");

    fireEvent.change(screen.getByTestId("form-connref-filter"), { target: { value: "Sample.Api" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
  });

  it("ProjectForm_ResolutionStrategySelector_FromCapabilities", () => {
    // p0345c: resolution is a strategy CHOICE from the backend's registry plus
    // a value with a per-strategy hint — no freetext guessing.
    render(<Harness />);
    const strategy = screen.getByTestId("form-field-resolution-strategy");
    expect(strategy.tagName).toBe("SELECT");
    for (const s of capabilities.resolutionStrategies) {
      expect(strategy.querySelector(`option[value="${s}"]`), `strategy ${s}`).not.toBeNull();
    }
    // No strategy → no value input (resolution stays null).
    expect(screen.queryByTestId("form-field-resolution-value")).toBeNull();

    fireEvent.change(strategy, { target: { value: "tag" } });
    const value = screen.getByTestId("form-field-resolution-value");
    expect(value).toHaveAttribute("placeholder", "e.g. checkout");

    fireEvent.change(value, { target: { value: "checkout" } });
    expect(screen.getByTestId("form-field-resolution-value")).toHaveValue("checkout");

    // Switching the strategy switches the hint.
    fireEvent.change(screen.getByTestId("form-field-resolution-strategy"), { target: { value: "to_address" } });
    expect(screen.getByTestId("form-field-resolution-value")).toHaveAttribute(
      "placeholder",
      "e.g. team@example.com",
    );
  });

  it("ProjectForm_PipelineSelect_FromCapabilities", () => {
    // The field once mislabeled "trigger" is a pipeline SELECT now.
    render(<Harness />);
    const pipeline = screen.getByTestId("form-field-pipeline");
    expect(pipeline.tagName).toBe("SELECT");
    expect(pipeline.querySelector('option[value="feature-implementation"]')).not.toBeNull();
    expect(pipeline.querySelector('option[value="api-scan"]')).not.toBeNull();
    fireEvent.change(pipeline, { target: { value: "api-scan" } });
    expect(screen.getByTestId("form-field-pipeline")).toHaveValue("api-scan");
  });

  it("ProjectForm_ConnScopedRepoRef_AddedViaConnectionPicker_CountsForIntegrity", async () => {
    // p0345b: the operator-shaped config references repos through a
    // connection ("conn/Name") — added via the connection picker + filter box,
    // and integrity treats the resolved connection as a valid ref.
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });

    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    await screen.findByTestId("form-connref-discovered-Sample.Api");
    fireEvent.change(screen.getByTestId("form-connref-filter"), { target: { value: "Sample.Api" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));

    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    expect(screen.getByTestId("wiring-repo-conn/Sample.Api")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");

    // Removing the conn-scoped ref drops integrity back to amber.
    fireEvent.click(screen.getByTestId("form-connref-remove-conn/Sample.Api"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");
  });

  it("ProjectForm_UnknownRepoRemoved_IntegrityReturnsAmber", () => {
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    // Deselecting the only repo drops integrity back to amber.
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");
  });
  it("ProjectForm_PicksTargetProjectRepoAndBothContexts", async () => {
    // 2026-09-14-620e: four of the five template fields are PICKED. The two context
    // names come from what the repositories declare, the target project and its repo
    // from the catalog the studio already holds.
    render(<Harness initial={{ repos: ["web"] }} />);
    fireEvent.click(screen.getByTestId("form-templates-add"));

    // The local context list is read for this project's own repos. Until it lands the
    // field is a text box, which is the same honest fallback an unreadable repo gets.
    await waitFor(() =>
      expect(screen.getByTestId("form-templates-0-context").tagName).toBe("SELECT"),
    );
    expect(mockedContexts).toHaveBeenCalledWith("proj", "web", expect.anything());
    expect(
      screen.getByTestId("form-templates-0-context").querySelector('option[value="server"]'),
    ).not.toBeNull();

    // The target project comes from the catalog.
    const target = screen.getByTestId("form-templates-0-project");
    expect(target.tagName).toBe("SELECT");
    expect(target.querySelector('option[value="refapp"]')).not.toBeNull();
    fireEvent.change(target, { target: { value: "refapp" } });

    // Its repo options are that project's own refs, not the whole repos catalog.
    const repo = screen.getByTestId("form-templates-0-repo");
    expect(repo.querySelector('option[value="api"]')).not.toBeNull();
    expect(repo.querySelector('option[value="web"]')).toBeNull();
    fireEvent.change(repo, { target: { value: "api" } });

    // And the target context list is read from THAT repo.
    await waitFor(() =>
      expect(screen.getByTestId("form-templates-0-templateContext").tagName).toBe("SELECT"),
    );
    expect(mockedContexts).toHaveBeenCalledWith("refapp", "api", expect.anything());
  });

  it("ProjectForm_RevisionStaysFreeText", async () => {
    // Nothing in the product enumerates refs, so the revision is the one typed field
    // and the form says where it is verified instead of shipping the gap quietly.
    render(<Harness />);
    fireEvent.click(screen.getByTestId("form-templates-add"));

    const revision = screen.getByTestId("form-templates-0-revision");
    expect(revision.tagName).toBe("INPUT");
    fireEvent.change(revision, { target: { value: "v2.1.0" } });
    expect(screen.getByTestId("form-templates-0-revision")).toHaveValue("v2.1.0");
  });

  it("ProjectForm_UnreadableRepo_DegradesTheFieldNotTheForm", async () => {
    // A credential that cannot reach the target is not a reason an operator cannot
    // finish editing a project: the field says why and accepts a typed name.
    mockedContexts.mockResolvedValue({ contexts: [], unreadableReason: "403 Forbidden" });
    render(<Harness initial={{ repos: ["web"] }} />);
    fireEvent.click(screen.getByTestId("form-templates-add"));

    const unreadable = await screen.findByTestId("form-templates-local-unreadable");
    expect(unreadable).toHaveTextContent("403 Forbidden");
    const context = screen.getByTestId("form-templates-0-context");
    expect(context.tagName).toBe("INPUT");
    fireEvent.change(context, { target: { value: "server" } });
    expect(screen.getByTestId("form-templates-0-context")).toHaveValue("server");
    // The rest of the form still works.
    expect(screen.getByTestId("form-ref-agent")).toBeInTheDocument();
  });

  it("ProjectForm_TemplatesUntouched_SurviveAnUnrelatedEdit", async () => {
    // The SERVER pins that a ProjectEntity omitting templates preserves the stored
    // declaration (ProjectTemplateStoreTests). What the form owns is this: editing a
    // field that has nothing to do with templates leaves a loaded list alone.
    const stored = [
      { context: "server", project: "refapp", repo: "api", templateContext: "server", revision: "v2.1.0" },
    ];
    render(<Harness initial={{ templates: stored }} />);

    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });

    await waitFor(() =>
      expect(screen.getByTestId("form-templates-0-templateContext")).toHaveValue("server"),
    );
    expect(screen.getByTestId("form-templates-0-project")).toHaveValue("refapp");
    expect(screen.getByTestId("form-templates-0-repo")).toHaveValue("api");
    expect(screen.getByTestId("form-templates-0-revision")).toHaveValue("v2.1.0");
  });
});
