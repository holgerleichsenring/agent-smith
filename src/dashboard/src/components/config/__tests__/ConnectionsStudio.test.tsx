import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";

// p0345b: the OPERATOR-shaped config — connections + connection-scoped project
// repo refs and an EMPTY repos catalog (the p0281a discovery world). The studio
// must render this world fully resolved: connections listed as first-class
// cards, projects with conn-scoped refs showing green wiring, nothing falsely
// dangling.

vi.mock("@/lib/configApi", () => {
  const agents = [
    { id: "azure", provider: "azure-openai", models: { coding: { model: "c" }, scan: { model: "s" } }, keySecret: "AOAI_KEY" },
  ];
  const trackers = [
    { id: "azdo", type: "azure", organization: "acme", project: "core", authSecret: "AZDO_PAT" },
  ];
  const connections = [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "AZDO_PAT", defaultBranch: "develop" },
  ];
  const projects = [
    {
      id: "sample",
      agent: "azure",
      tracker: "azdo",
      repos: ["conn/Sample.Api", "conn/Sample.Web"],
      pipeline: "feature-implementation",
      pipelines: ["feature-implementation"],
      resolution: null,
    },
  ];
  const secrets = [{ id: "AOAI_KEY" }, { id: "AZDO_PAT" }];
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
    reposApi: client([]), // the operator has NO legacy repos catalog
    projectsApi: client(projects),
    mcpServersApi: client([]),
    secretsApi: client(secrets),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn().mockResolvedValue(""),
    // p0345c: the connection form renders its fields from the capabilities
    // descriptor once a type is picked; orgLabel names the org field.
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [],
      connectionTypes: [
        {
          type: "azure-devops",
          orgLabel: "organization",
          fields: [
            { key: "organization", label: "organization", required: true },
            { key: "project", label: "project", required: true },
            { key: "defaultBranch", label: "default branch", required: false },
          ],
        },
      ],
      agentProviders: ["azure-openai"],
      resolutionStrategies: ["tag"],
      pipelines: ["feature-implementation"],
    }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({
      discoveredAt: "2026-07-17T08:00:00Z",
      repos: [{ name: "Sample.Api", defaultBranch: "develop" }],
    }),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("ConfigStudio connections (p0345b)", () => {
  it("Studio_ConnectionsTab_RendersCatalogCardWithFieldsAndAuthChip", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="connections" /></ConfigCatalogProvider>);
    const card = await screen.findByTestId("config-card-connections-conn");
    expect(card).toHaveTextContent("azure-devops");
    expect(card).toHaveTextContent("acme");
    expect(card).toHaveTextContent("core");
    expect(card).toHaveTextContent("develop");
    // The auth secret resolves against the secrets catalog.
    expect(screen.getByTestId("config-card-connection-auth-conn")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-new-connections")).toBeInTheDocument();
  });

  it("Studio_NewConnection_FormHasFieldsAndSecretPicker", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="connections" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-new-connections");
    fireEvent.click(screen.getByTestId("config-new-connections"));

    expect(screen.getByTestId("config-drawer")).toBeInTheDocument();
    // p0345c: type is a dropdown from capabilities; its field set renders only
    // once a type is picked.
    const type = await screen.findByTestId("form-field-type");
    expect(type.tagName).toBe("SELECT");
    await waitFor(() => expect(type.querySelector('option[value="azure-devops"]')).not.toBeNull());
    expect(screen.queryByTestId("form-field-organization")).toBeNull();

    fireEvent.change(type, { target: { value: "azure-devops" } });
    expect(screen.getByTestId("form-field-organization")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-project")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-defaultBranch")).toBeInTheDocument();
    // authSecret is a pick-only secret FK, never free text.
    const secret = screen.getByTestId("form-ref-authSecret");
    expect(secret.tagName).toBe("SELECT");
    expect(secret.querySelector('option[value="AZDO_PAT"]')).not.toBeNull();
  });

  it("Studio_OperatorShapedConfig_ProjectConnRefsResolve_NothingFalselyDangling", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-sample");
    // Both conn-scoped refs resolve via the connections catalog even though
    // the repos catalog is empty.
    expect(screen.getByTestId("config-card-repo-sample-conn/Sample.Api")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-repo-sample-conn/Sample.Web")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-agent-sample")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-card-tracker-sample")).toHaveAttribute("data-resolved", "true");
  });

  it("Studio_OperatorShapedProject_EditDrawer_IntegrityConfirmed", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="projects" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-card-projects-sample");
    fireEvent.click(screen.getByTestId("config-card-edit-sample"));

    // The wiring preview resolves the conn-scoped refs green and Save is open.
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    expect(screen.getByTestId("wiring-repo-conn/Sample.Api")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("config-drawer-save")).not.toBeDisabled();
    // The existing conn refs are visible and manageable in the form.
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Web")).toBeInTheDocument();
  });
});
