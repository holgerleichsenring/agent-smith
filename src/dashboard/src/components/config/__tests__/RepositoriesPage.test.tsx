import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";
import { refMatches } from "../RepoInventory";

// p0345c: the Repositories page shows BOTH worlds — the per-connection
// DISCOVERED inventory (read-only, with referenced-by badges and the honest
// not-discovered state) and the legacy standalone catalog below it.

vi.mock("@/lib/configApi", () => {
  const connections = [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "PAT", defaultBranch: "main" },
    { id: "cold", type: "azure-devops", organization: "acme", project: "ops", authSecret: "PAT", defaultBranch: "main" },
  ];
  const repos = [{ id: "legacy", name: "Legacy.Server", branch: "main" }];
  const projects = [
    {
      id: "sample",
      agent: "a",
      tracker: "t",
      repos: ["conn/Sample.Api", "legacy"],
      pipeline: "feature",
      pipelines: [],
      resolution: null,
    },
    {
      id: "wild",
      agent: "a",
      tracker: "t",
      repos: ["conn/*"],
      pipeline: "feature",
      pipelines: [],
      resolution: null,
    },
  ];
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  });
  return {
    agentsApi: client([]),
    trackersApi: client([]),
    connectionsApi: client(connections),
    reposApi: client(repos),
    projectsApi: client(projects),
    mcpServersApi: client([]),
    secretsApi: client([]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [],
      connectionTypes: [],
      agentProviders: [],
      resolutionStrategies: [],
      pipelines: [],
    }),
    // conn has a warm discovery cache; cold was never discovered.
    fetchConnectionRepos: vi.fn().mockImplementation((id: string) =>
      Promise.resolve(
        id === "conn"
          ? {
              discoveredAt: "2026-07-17T08:00:00Z",
              repos: [
                { name: "Sample.Api", defaultBranch: "main" },
                { name: "Sample.Web", defaultBranch: null },
              ],
            }
          : { discoveredAt: null, repos: [] },
      ),
    ),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("Repositories page (p0345c)", () => {
  it("Repositories_ShowsDiscoveredInventory_WithReferencedBy", async () => {
    render(<ConfigCatalogProvider><ConfigStudio section="repos" /></ConfigCatalogProvider>);

    // The discovered world: one section per connection, read-only rows.
    const api = await screen.findByTestId("repo-inventory-conn-Sample.Api");
    expect(api).toHaveTextContent("Sample.Api");
    expect(screen.getByTestId("repo-inventory-conn-Sample.Web")).toBeInTheDocument();

    // Referenced-by badges: exact ref on `sample`, wildcard ref on `wild`.
    expect(screen.getByTestId("repo-referenced-conn-Sample.Api-sample")).toHaveTextContent(
      "referenced by sample",
    );
    expect(screen.getByTestId("repo-referenced-conn-Sample.Api-wild")).toBeInTheDocument();
    expect(screen.getByTestId("repo-referenced-conn-Sample.Web-wild")).toBeInTheDocument();
    // Sample.Web is NOT referenced exactly by `sample`.
    expect(screen.queryByTestId("repo-referenced-conn-Sample.Web-sample")).toBeNull();

    // The never-discovered connection states it honestly.
    expect(await screen.findByTestId("repo-inventory-undiscovered-cold")).toHaveTextContent(
      "not discovered yet",
    );

    // The legacy standalone catalog still renders below, as editable cards.
    expect(screen.getByTestId("config-card-repos-legacy")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Discovered per connection" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Standalone catalog" })).toBeInTheDocument();
  });

  it("RefMatches_ExactAndGlob_PlainRefsNever", () => {
    expect(refMatches("conn/Sample.Api", "conn", "Sample.Api")).toBe(true);
    expect(refMatches("conn/*", "conn", "Anything")).toBe(true);
    expect(refMatches("conn/Sample.*", "conn", "Sample.Api")).toBe(true);
    expect(refMatches("conn/Sample.*", "conn", "Other.Api")).toBe(false);
    expect(refMatches("other/Sample.Api", "conn", "Sample.Api")).toBe(false);
    expect(refMatches("legacy", "conn", "legacy")).toBe(false);
  });
});
