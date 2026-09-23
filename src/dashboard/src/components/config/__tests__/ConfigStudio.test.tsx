import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";

// The factory is hoisted above imports, so all fixtures live inside it.
vi.mock("@/lib/configApi", () => {
  const agents = [
    { id: "gpt5", provider: "openai", models: { coding: { model: "c" }, scan: { model: "s" } }, keySecret: "OPENAI_KEY" },
    // p0343b: an entry whose roles are NOT the conventional coding/scan pair,
    // and whose key ref is honestly absent.
    {
      id: "claude",
      provider: "anthropic",
      models: { primary: { model: "opus" }, scout: { model: "haiku" }, planning: { model: "sonnet" } },
      keySecret: null,
    },
    // a key ref NAMING a secret that is missing from the catalog → dangling.
    { id: "broken-key", provider: "openai", models: { coding: { model: "c" } }, keySecret: "GHOST_KEY" },
  ];
  const trackers = [{ id: "azdo", type: "azure", organization: "acme", project: "core", authSecret: "AZDO_PAT" }];
  const connections = [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "AZDO_PAT", defaultBranch: "main" },
  ];
  const repos = [{ id: "web", name: "web", branch: "main" }];
  const secrets = [{ id: "OPENAI_KEY" }, { id: "AZDO_PAT" }];
  const projects = [
    // p0345c: `pipeline` (the truth-fixed rename of "trigger") + resolution.
    { id: "checkout", agent: "gpt5", tracker: "azdo", repos: ["web"], pipeline: "feature", pipelines: ["feature"], resolution: { strategy: "tag", value: "checkout" }, templates: [{ context: "server", project: "refapp", repo: "web", templateContext: "server" }] },
    // a project with a dangling agent ref, to prove the card flags it
    { id: "broken", agent: "ghost", tracker: "azdo", repos: ["web"], pipeline: "feature", pipelines: [], resolution: null },
  ];
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn().mockResolvedValue(rows[0]),
    update: vi.fn().mockResolvedValue(rows[0]),
    remove: vi.fn().mockResolvedValue(undefined),
  });
  return {
    agentsApi: client(agents),
    trackersApi: client(trackers),
    connectionsApi: client(connections),
    reposApi: client(repos),
    projectsApi: client(projects),
    mcpServersApi: client([]),
    secretsApi: client(secrets),
    fetchChanges: vi.fn().mockResolvedValue([]),
    // 2026-09-22-6968: the catalog load reads the inherited-sandbox projection beside
    // its seven lists; a wholesale module mock has to declare it.
    fetchInheritedSandbox: vi.fn().mockResolvedValue({
      processWide: {
        toolchainImage: { value: null, source: "run-resolved" },
        stepTimeoutSeconds: { value: 900, source: "global-default" },
        runCommandTimeoutSeconds: { value: 300, source: "global-default" },
        agentRegistry: { value: "ghcr.io/example", source: "global-default" },
        agentVersion: { value: "0.50.0", source: "global-default" },
      },
      projects: {},
    }),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn().mockResolvedValue("agents:\n  - id: gpt5\n"),
    validateProjectDraft: vi.fn().mockResolvedValue([]),
    validateTrackerDraft: vi.fn().mockResolvedValue([]),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [{ type: "azure", fields: [{ key: "organization", label: "organization", required: true }] }],
      connectionTypes: [{ type: "azure-devops", orgLabel: "organization", fields: [] }],
      agentProviders: ["openai", "anthropic"],
      resolutionStrategies: ["tag", "area_path"],
      permissions: [],
      builtInRoles: [],
      pipelines: ["feature", "api-scan"],
    }),
    // 2026-09-14-620e: the template form reads context names live; a wholesale
    // module mock has to declare it or the form throws on mount.
    fetchProjectContexts: vi.fn().mockResolvedValue({ contexts: [], unreadableReason: null }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("ConfigStudio", () => {
  it("ConfigStudio_ProjectsSection_RendersTitleRowCardsAndNewButton", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-checkout");
    // p0343b: the mock's title row — entity title + subtitle + green New button;
    // the tab row is gone (the rail catalog switches sections now).
    expect(screen.getByRole("heading", { name: "Projects" })).toBeInTheDocument();
    expect(screen.getByTestId("config-new-projects")).toBeInTheDocument();
    expect(screen.queryByTestId("config-tabs")).not.toBeInTheDocument();
  });

  it("ProjectCard_Expanded_GraphMarksTheDanglingRef", async () => {
    // 2026-09-16-bedc: this was the chip row's test. The chips are gone and the graph is the
    // wiring — but the thing it proved is still worth proving, so it moved rather than being
    // deleted: a project naming an agent no catalog carries is SHOWN as broken on the card.
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-broken");

    fireEvent.click(screen.getByTestId("config-card-disclosure-broken"));

    expect(screen.getByTestId("graph-node-agent-broken")).toHaveAttribute("data-coloured", "true");
    expect(screen.getByTestId("graph-node-tracker-broken")).toHaveAttribute("data-coloured", "false");
  });

  it("AgentCard_ListsPresentModelRoles_NoPhantomDashes", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-agents-claude");
    // The roles ACTUALLY present render — primary/scout/planning …
    expect(screen.getByTestId("config-card-model-claude-primary")).toHaveTextContent("opus");
    expect(screen.getByTestId("config-card-model-claude-scout")).toHaveTextContent("haiku");
    expect(screen.getByTestId("config-card-model-claude-planning")).toHaveTextContent("sonnet");
    // … and NO phantom coding/scan dashes for roles the entry does not have.
    expect(screen.queryByTestId("config-card-model-claude-coding")).not.toBeInTheDocument();
    expect(screen.queryByTestId("config-card-model-claude-scan")).not.toBeInTheDocument();
  });

  it("AgentCard_KeySecret_NullIsNeutral_DanglingRefIsRose", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-agents-claude");
    // No key ref at all → honest neutral "key —", never rose.
    expect(screen.getByTestId("config-card-key-claude")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-key-claude")).toHaveTextContent("key");
    // A ref naming a MISSING secret → rose.
    expect(screen.getByTestId("config-card-key-broken-key")).toHaveAttribute("data-resolved", "false");
    // A ref naming an existing secret → neutral.
    expect(screen.getByTestId("config-card-key-gpt5")).toHaveAttribute("data-resolved", "true");
  });

  it("ConfigStudio_ExportButton_FetchesExportYml", async () => {
    const { fetchConfigExportYml } = await import("@/lib/configApi");
    // jsdom has no createObjectURL — stub the download plumbing.
    const createObjectURL = vi.fn(() => "blob:fake");
    const revokeObjectURL = vi.fn();
    vi.stubGlobal("URL", Object.assign(URL, { createObjectURL, revokeObjectURL }));
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});

    render(<ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-thesis-note");
    fireEvent.click(screen.getByTestId("config-export-yml"));

    await waitFor(() => expect(fetchConfigExportYml).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(click).toHaveBeenCalled());
    expect(createObjectURL).toHaveBeenCalled();
    click.mockRestore();
    vi.unstubAllGlobals();
  });

  it("ConfigStudio_NewProject_OpensDrawerWithCatalogPickers", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-new-projects");
    fireEvent.click(screen.getByTestId("config-new-projects"));

    expect(screen.getByTestId("config-drawer")).toBeInTheDocument();
    // Ref pickers are populated from the loaded catalog.
    const agent = screen.getByTestId("form-ref-agent");
    expect(agent.querySelector('option[value="gpt5"]')).not.toBeNull();
    // A fresh project has unresolved integrity → Save is blocked.
    expect(screen.getByTestId("config-drawer-save")).toBeDisabled();
    expect(screen.getByTestId("config-drawer-blocked")).toBeInTheDocument();
  });

  it("ConfigStudio_CompleteProject_EnablesSaveAndPersists", async () => {
    const { projectsApi } = await import("@/lib/configApi");
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-new-projects");
    fireEvent.click(screen.getByTestId("config-new-projects"));

    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "newproj" } });
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    // 2026-09-16-74a2: the repo pickers live in the form's repos section.
    fireEvent.click(screen.getByTestId("form-tab-repos"));
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));

    const save = screen.getByTestId("config-drawer-save");
    expect(save).not.toBeDisabled();
    fireEvent.click(save);
    expect(projectsApi.create).toHaveBeenCalledTimes(1);
  });

  it("ConfigStudio_ProjectDrawer_ThreadsTheInheritedSandboxValuesIntoTheSandboxTab", async () => {
    // 2026-09-22-6968: before this phase the studio could see no resolved projection at
    // all — it loaded a catalog of editable entities and nothing else. This is the thread
    // from the studio's own load down to the sixth tab's placeholders.
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-checkout");
    fireEvent.click(screen.getByTestId("config-card-edit-checkout"));
    fireEvent.click(screen.getByTestId("form-tab-sandbox"));

    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).toHaveAttribute(
      "placeholder",
      "900",
    );
    expect(screen.getByTestId("form-field-sandbox-agentRegistry")).toHaveAttribute(
      "placeholder",
      "ghcr.io/example",
    );
  });

  it("ConfigStudio_SecretsSection_ShowsRedactionNeverValueInput", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="secrets" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-secrets-OPENAI_KEY");
    fireEvent.click(screen.getByTestId("config-new-secrets"));
    // The secret form carries a redaction bar and only an id field — no value input.
    expect(screen.getByTestId("secret-redaction-bar")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-id")).toBeInTheDocument();
    expect(screen.queryByTestId("form-field-value")).toBeNull();
  });

  it("ConfigStudio_ChangesSection_RendersAuditView", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="changes" /></ConfigCatalogProvider>);
    expect(await screen.findByTestId("config-changes")).toBeInTheDocument();
    // Changes has no New button (nothing to create in an audit trail).
    expect(screen.queryByTestId("config-new-changes")).not.toBeInTheDocument();
  });
  it("Catalog_EveryKind_IsSortedById", async () => {
    // 2026-09-15-9b3e: the store returns a SELECT with no ORDER BY, so what arrives is not
    // insertion order — it is no order at all. The fixture lists projects as
    // checkout, broken; the cards must read broken, checkout.
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-checkout");

    const ids = screen
      .queryAllByTestId(/^config-card-projects-/)
      .map((el) => el.getAttribute("data-testid")!.replace("config-card-projects-", ""));

    expect(ids).toEqual([...ids].sort((a, b) => a.localeCompare(b)));
    expect(ids[0]).toBe("broken");
  });

  it("ProjectCard_WithTemplates_NamesThem", async () => {
    // A template was declarable and invisible on the card that summarises the project.
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);

    const line = await screen.findByTestId("config-project-templates-checkout");
    expect(line).toHaveTextContent("built after refapp");
    // A project that declares none says nothing rather than "0 templates".
    expect(screen.queryByTestId("config-project-templates-broken")).toBeNull();
  });

  it("OtherCards_KeepTheirTypeBadge", async () => {
    // Only the project's badge had nothing to say. An agent's names its provider — the one
    // place that fact appears on the card.
    render(<ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>);

    const badge = await screen.findByTestId("config-card-badge-gpt5");
    expect(badge).toHaveTextContent("openai");
  });

  it("OtherCards_Root_StillOfferAPointer", async () => {
    // Their whole card still opens the editor, so the pointer is still true.
    const { container } = render(
      <ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-agents-gpt5");

    expect(container.querySelector(".ecard")).not.toHaveClass("inert");
  });
});
