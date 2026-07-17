import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";

// The factory is hoisted above imports, so all fixtures live inside it.
vi.mock("@/lib/configApi", () => {
  const agents = [
    { id: "gpt5", provider: "openai", models: { coding: "c", scan: "s" }, keySecret: "OPENAI_KEY" },
    // p0343b: an entry whose roles are NOT the conventional coding/scan pair,
    // and whose key ref is honestly absent.
    { id: "claude", provider: "anthropic", models: { primary: "opus", scout: "haiku", planning: "sonnet" }, keySecret: null },
    // a key ref NAMING a secret that is missing from the catalog → dangling.
    { id: "broken-key", provider: "openai", models: { coding: "c" }, keySecret: "GHOST_KEY" },
  ];
  const trackers = [{ id: "azdo", type: "azure", org: "acme", project: "core", authSecret: "AZDO_PAT" }];
  const connections = [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "AZDO_PAT", defaultBranch: "main" },
  ];
  const repos = [{ id: "web", name: "web", branch: "main" }];
  const secrets = [{ id: "OPENAI_KEY" }, { id: "AZDO_PAT" }];
  const projects = [
    { id: "checkout", agent: "gpt5", tracker: "azdo", repos: ["web"], trigger: "ready", pipelines: ["feature"] },
    // a project with a dangling agent ref, to prove the card flags it
    { id: "broken", agent: "ghost", tracker: "azdo", repos: ["web"], trigger: "ready", pipelines: [] },
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
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn().mockResolvedValue("agents:\n  - id: gpt5\n"),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("ConfigStudio", () => {
  it("ConfigStudio_ProjectsSection_RendersTitleRowCardsAndNewButton", async () => {
    render(<ConfigStudio section="projects" />);
    await screen.findByTestId("config-card-projects-checkout");
    // p0343b: the mock's title row — entity title + subtitle + green New button;
    // the tab row is gone (the rail catalog switches sections now).
    expect(screen.getByRole("heading", { name: "Projects" })).toBeInTheDocument();
    expect(screen.getByTestId("config-new-projects")).toBeInTheDocument();
    expect(screen.queryByTestId("config-tabs")).not.toBeInTheDocument();
  });

  it("ProjectCard_WiresRow_RendersResolvedChips", async () => {
    render(<ConfigStudio section="projects" />);
    await screen.findByTestId("config-card-projects-checkout");
    // agent → [project] ← tracker · repo — resolved chips neutral, project green.
    expect(screen.getByTestId("config-card-agent-checkout")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-tracker-checkout")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-repo-checkout-web")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-project-chip-checkout")).toHaveTextContent("checkout");
    // The dangling agent ref on the broken project renders rose (unresolved).
    expect(screen.getByTestId("config-card-agent-broken")).toHaveAttribute("data-resolved", "false");
  });

  it("AgentCard_ListsPresentModelRoles_NoPhantomDashes", async () => {
    render(<ConfigStudio section="agents" />);
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
    render(<ConfigStudio section="agents" />);
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

    render(<ConfigStudio section="agents" />);
    await screen.findByTestId("config-thesis-note");
    fireEvent.click(screen.getByTestId("config-export-yml"));

    await waitFor(() => expect(fetchConfigExportYml).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(click).toHaveBeenCalled());
    expect(createObjectURL).toHaveBeenCalled();
    click.mockRestore();
    vi.unstubAllGlobals();
  });

  it("ConfigStudio_NewProject_OpensDrawerWithCatalogPickers", async () => {
    render(<ConfigStudio section="projects" />);
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
    render(<ConfigStudio section="projects" />);
    await screen.findByTestId("config-new-projects");
    fireEvent.click(screen.getByTestId("config-new-projects"));

    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "newproj" } });
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));

    const save = screen.getByTestId("config-drawer-save");
    expect(save).not.toBeDisabled();
    fireEvent.click(save);
    expect(projectsApi.create).toHaveBeenCalledTimes(1);
  });

  it("ConfigStudio_SecretsSection_ShowsRedactionNeverValueInput", async () => {
    render(<ConfigStudio section="secrets" />);
    await screen.findByTestId("config-card-secrets-OPENAI_KEY");
    fireEvent.click(screen.getByTestId("config-new-secrets"));
    // The secret form carries a redaction bar and only an id field — no value input.
    expect(screen.getByTestId("secret-redaction-bar")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-id")).toBeInTheDocument();
    expect(screen.queryByTestId("form-field-value")).toBeNull();
  });

  it("ConfigStudio_ChangesSection_RendersAuditView", async () => {
    render(<ConfigStudio section="changes" />);
    expect(await screen.findByTestId("config-changes")).toBeInTheDocument();
    // Changes has no New button (nothing to create in an audit trail).
    expect(screen.queryByTestId("config-new-changes")).not.toBeInTheDocument();
  });
});
