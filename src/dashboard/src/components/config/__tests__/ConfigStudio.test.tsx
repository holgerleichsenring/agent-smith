import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";

// The factory is hoisted above imports, so all fixtures live inside it.
vi.mock("@/lib/configApi", () => {
  const agents = [
    { id: "gpt5", provider: "openai", models: { coding: "c", scan: "s" }, keySecret: "OPENAI_KEY" },
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
  };
});

beforeEach(() => vi.clearAllMocks());

describe("ConfigStudio", () => {
  it("ConfigStudio_ProjectsSection_RendersCardsAndNewButton", async () => {
    render(<ConfigStudio section="projects" />);
    await screen.findByTestId("config-card-projects-checkout");
    expect(screen.getByTestId("config-new-projects")).toBeInTheDocument();
    // The dangling agent ref on the broken project renders rose (unresolved).
    expect(screen.getByTestId("config-card-agent-broken")).toHaveAttribute("data-resolved", "false");
    expect(screen.getByTestId("config-card-agent-checkout")).toHaveAttribute("data-resolved", "true");
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
  });
});
