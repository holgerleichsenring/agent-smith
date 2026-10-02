import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";

// 2026-10-01-7f7aa: a design source is a catalog entry whose token is a secret picked by
// NAME. The tab lists sources with the secret's name, and a new source is saved carrying
// that name and nothing that could be a value.

vi.mock("@/lib/configApi", () => {
  const designSources = [{ id: "brand", vendor: "figma", authSecret: "FIGMA_TOKEN", displayName: "Brand library" }];
  const secrets = [{ id: "FIGMA_TOKEN" }, { id: "OTHER" }];
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn().mockImplementation(async (body: T) => body),
    update: vi.fn().mockImplementation(async (_id: string, body: T) => body),
    remove: vi.fn().mockResolvedValue(undefined),
  });
  return {
    agentsApi: client([]),
    trackersApi: client([]),
    connectionsApi: client([]),
    reposApi: client([]),
    projectsApi: client([]),
    mcpServersApi: client([]),
    designSourcesApi: client(designSources),
    secretsApi: client(secrets),
    fetchChanges: vi.fn().mockResolvedValue([]),
    fetchInheritedSandbox: vi.fn().mockResolvedValue(null),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn().mockResolvedValue(""),
    validateProjectDraft: vi.fn().mockResolvedValue([]),
    validateTrackerDraft: vi.fn().mockResolvedValue([]),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [],
      connectionTypes: [],
      agentProviders: [],
      resolutionStrategies: [],
      permissions: [],
      builtInRoles: [],
      pipelines: [],
    }),
    fetchProjectContexts: vi.fn().mockResolvedValue({ contexts: [], unreadableReason: null }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("ConfigStudio design sources (2026-10-01-7f7aa)", () => {
  it("lists a source with its vendor and the secret's name, resolved against the secrets", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="design-sources" /></ConfigCatalogProvider>);
    const card = await screen.findByTestId("config-card-design-sources-brand");
    expect(card).toHaveTextContent("figma");
    expect(card).toHaveTextContent("Brand library");
    const auth = screen.getByTestId("config-card-design-source-auth-brand");
    expect(auth).toHaveTextContent("FIGMA_TOKEN");
    expect(auth).toHaveAttribute("data-resolved", "true");
  });

  it("creates a source showing the secret name only", async () => {
    const { designSourcesApi } = await import("@/lib/configApi");
    render(<ConfigCatalogProvider><ConfigStudio section="design-sources" /></ConfigCatalogProvider>);
    await screen.findByTestId("config-new-design-sources");
    fireEvent.click(screen.getByTestId("config-new-design-sources"));

    expect(screen.getByTestId("config-drawer-save")).toBeDisabled();
    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "product" } });
    const secret = screen.getByTestId("form-ref-authSecret");
    expect(secret.tagName).toBe("SELECT");
    fireEvent.change(secret, { target: { value: "FIGMA_TOKEN" } });

    const save = screen.getByTestId("config-drawer-save");
    await waitFor(() => expect(save).not.toBeDisabled());
    fireEvent.click(save);

    await waitFor(() => expect(designSourcesApi.create).toHaveBeenCalledTimes(1));
    expect(designSourcesApi.create).toHaveBeenCalledWith(
      { id: "product", vendor: "figma", authSecret: "FIGMA_TOKEN" });
  });
});
