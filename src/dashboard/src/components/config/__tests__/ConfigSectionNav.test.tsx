import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { ConfigSectionNav } from "@/components/config/ConfigSectionNav";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// p0343b: the configuration sections — the entity catalog with live counts, the settings,
// access and this installation. 2026-11-16-0440c: they left the app rail for the /config
// layout; the profile is the way in and "Back to work" the way out.

const renderNav = () =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <ConfigCatalogProvider>
        <ConfigSectionNav />
      </ConfigCatalogProvider>
    </EventStoreProvider>,
  );

const usePathname = vi.fn(() => "/config");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname() }));

const HUB = {
  client: {
    systemEvents: { add: () => () => {} },
    subscribeSystem: () => Promise.resolve(() => {}),
  },
  connectionState: 1,
  overview: null,
  systemActivity: null,
};
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => HUB }));


// The factory is hoisted above imports, so all fixtures live inside it.
vi.mock("@/lib/configApi", () => {
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  });
  return {
    agentsApi: client([{ id: "gpt5" }, { id: "azure" }]),
    trackersApi: client([{ id: "azdo" }]),
    connectionsApi: client([{ id: "conn" }]),
    reposApi: client([]),
    projectsApi: client([{ id: "checkout" }, { id: "sample" }, { id: "ops" }]),
    mcpServersApi: client([]),
    designSourcesApi: client([]),
    secretsApi: client([{ id: "KEY" }]),
    fetchChanges: vi.fn().mockResolvedValue([{ id: "c1" }, { id: "c2" }]),
    // 2026-09-22-6968: the catalog load reads the inherited-sandbox projection beside
    // its seven lists; a wholesale module mock has to declare it.
    fetchInheritedSandbox: vi.fn().mockResolvedValue({
      processWide: {
        toolchainImage: { value: null, source: "run-resolved" },
        stepTimeoutSeconds: { value: 900, source: "global-default" },
        runCommandTimeoutSeconds: { value: 300, source: "global-default" },
        agentRegistry: { value: "ghcr.io/example", source: "global-default" },
        agentVersion: { value: "0.50.0", source: "global-default" },
        holdSeconds: { value: 180, source: "code-default" },
      },
      projects: {},
    }),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
  };
});

beforeEach(() => usePathname.mockReturnValue("/config"));

describe("ConfigSectionNav", () => {
  it("ConfigNav_ConfigRoute_ShowsCatalogWithCounts", async () => {
    renderNav();
    expect(screen.getByTestId("section-nav-group-Catalog")).toBeInTheDocument();
    // Live entity counts from the same list clients the studio loads.
    expect(await screen.findByTestId("section-nav-count-Projects")).toHaveTextContent("3");
    expect(screen.getByTestId("section-nav-count-Agents")).toHaveTextContent("2");
    expect(screen.getByTestId("section-nav-count-Trackers")).toHaveTextContent("1");
    expect(screen.getByTestId("section-nav-count-Repositories")).toHaveTextContent("0");
    expect(screen.getByTestId("section-nav-count-Connections")).toHaveTextContent("1");
    expect(screen.getByTestId("section-nav-count-MCP servers")).toHaveTextContent("0");
    expect(screen.getByTestId("section-nav-count-Secrets")).toHaveTextContent("1");
  });

  it("ConfigNav_ConfigRoute_CatalogItemsRouteToTheirSection", async () => {
    renderNav();
    await screen.findByTestId("section-nav-count-Projects");
    expect(screen.getByTestId("section-nav-item-Projects")).toHaveAttribute("href", "/config/projects");
    expect(screen.getByTestId("section-nav-item-Secrets")).toHaveAttribute("href", "/config/secrets");
  });

  it("ConfigNav_ConfigRoute_ShowsSettingsGroupWithTypedFormRoutes", async () => {
    renderNav();
    expect(screen.getByTestId("section-nav-group-Settings")).toBeInTheDocument();
    // One entry per settings singleton, routing to its typed form.
    expect(screen.getByTestId("section-nav-item-Orchestrator")).toHaveAttribute(
      "href",
      "/config/settings/orchestrator",
    );
    expect(screen.getByTestId("section-nav-item-Pipeline cost cap")).toHaveAttribute(
      "href",
      "/config/settings/pipeline_cost_cap",
    );
  });

  // 2026-10-13-8762w: the spec review and the series are settings singletons, listed right after Dialogue.
  it("SettingsStudio_ConfigNav_ListsSpecReviewAndSeries", async () => {
    renderNav();
    const review = screen.getByTestId("section-nav-item-Spec review");
    const series = screen.getByTestId("section-nav-item-Series");
    expect(review).toHaveAttribute("href", "/config/settings/spec_review");
    expect(series).toHaveAttribute("href", "/config/settings/series");
    const order = (el: Element) => [...document.querySelectorAll("[data-testid^='section-nav-item-']")].indexOf(el);
    expect(order(review)).toBe(order(screen.getByTestId("section-nav-item-Dialogue")) + 1);
    expect(order(series)).toBe(order(review) + 1);
  });

  it("ConfigNav_SettingsRoute_MarksTheActiveSettingsItem", async () => {
    usePathname.mockReturnValue("/config/settings/orchestrator");
    renderNav();
    expect(screen.getByTestId("section-nav-item-Orchestrator")).toHaveAttribute("data-active", "true");
  });

  it("ConfigNav_ConfigRoute_TheInstallationGroupShowsTheChangesCount", async () => {
    renderNav();
    expect(screen.getByTestId("section-nav-group-This installation")).toBeInTheDocument();
    const changes = await screen.findByTestId("section-nav-count-Changes");
    expect(changes).toHaveTextContent("2");
    expect(screen.getByTestId("section-nav-item-Changes")).toHaveAttribute("href", "/config/changes");
  });

  it("ConfigNav_TheInstallationReadOut_RendersInConfigMode", async () => {
    usePathname.mockReturnValue("/config/installation");
    renderNav();
    const item = screen.getByTestId("section-nav-item-Installation");
    expect(item).toHaveAttribute("href", "/config/installation");
    expect(item).toHaveAttribute("data-active", "true");
  });

  it("ConfigNav_TheConnectionCheck_RendersInConfigMode", async () => {
    usePathname.mockReturnValue("/config/connection-check");
    renderNav();
    const item = screen.getByTestId("section-nav-item-Connection check");
    // NOT /config/connections — that path lists the connection catalog.
    expect(item).toHaveAttribute("href", "/config/connection-check");
    expect(item).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("section-nav-item-Connections")).toHaveAttribute(
      "data-active",
      "false",
    );
  });

  it("ConfigNav_ConfigMode_NamesTheAccessEntryPermissions", async () => {
    renderNav();
    expect(screen.getByTestId("section-nav-item-Permissions")).toHaveAttribute(
      "href",
      "/config/access",
    );
    expect(screen.queryByTestId("section-nav-item-Who may do what")).toBeNull();
  });

  it("ConfigNav_OffersTheWayBackToWork", async () => {
    renderNav();
    expect(screen.getByTestId("config-nav-back")).toHaveAttribute("href", "/");
  });

  it("ConfigNav_ConfigSubroute_MarksTheSectionItsOn", async () => {
    usePathname.mockReturnValue("/config/projects");
    renderNav();
    expect(await screen.findByTestId("section-nav-item-Projects")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("section-nav-item-Agents")).toHaveAttribute("data-active", "false");
  });
});
