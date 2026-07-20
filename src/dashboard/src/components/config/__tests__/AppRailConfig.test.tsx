import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { AppRail } from "@/components/shell/AppRail";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// p0343b: on /config routes the rail flips into CATALOG mode — the entity list
// with live counts replaces monitor/system/rollups, plus HISTORY (Changes).
// The segmented toggle is the way between the two surfaces.

const renderRail = () =>
  render(
    <EventStoreProvider store={silentEventStore()}>
      <ConfigCatalogProvider>
        <AppRail />
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

// p0347: AppRail always fetches the open-PR count on mount (even in config mode);
// mock it so config-rail tests stay network-free.
vi.mock("@/lib/pullRequestsApi", () => ({
  fetchPullRequests: vi.fn().mockResolvedValue([]),
}));

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
    secretsApi: client([{ id: "KEY" }]),
    fetchChanges: vi.fn().mockResolvedValue([{ id: "c1" }, { id: "c2" }]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
  };
});

beforeEach(() => usePathname.mockReturnValue("/config"));

describe("AppRail Configuration mode", () => {
  it("AppRail_ConfigRoute_ShowsCatalogWithCounts", async () => {
    renderRail();
    expect(screen.getByTestId("app-rail")).toHaveAttribute("data-mode", "config");
    expect(screen.getByTestId("app-rail-section-Catalog")).toBeInTheDocument();
    // Live entity counts from the same list clients the studio loads.
    expect(await screen.findByTestId("app-rail-count-Projects")).toHaveTextContent("3");
    expect(screen.getByTestId("app-rail-count-Agents")).toHaveTextContent("2");
    expect(screen.getByTestId("app-rail-count-Trackers")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-Repositories")).toHaveTextContent("0");
    expect(screen.getByTestId("app-rail-count-Connections")).toHaveTextContent("1");
    expect(screen.getByTestId("app-rail-count-MCP servers")).toHaveTextContent("0");
    expect(screen.getByTestId("app-rail-count-Secrets")).toHaveTextContent("1");
    // Runs-mode sections are gone in config mode.
    expect(screen.queryByTestId("app-rail-section-Monitor")).not.toBeInTheDocument();
    expect(screen.queryByTestId("app-rail-section-System")).not.toBeInTheDocument();
  });

  it("AppRail_ConfigRoute_CatalogItemsRouteToTheirSection", async () => {
    renderRail();
    await screen.findByTestId("app-rail-count-Projects");
    expect(screen.getByTestId("app-rail-item-Projects")).toHaveAttribute("href", "/config/projects");
    expect(screen.getByTestId("app-rail-item-Secrets")).toHaveAttribute("href", "/config/secrets");
  });

  it("AppRail_ConfigRoute_ShowsSettingsGroupWithTypedFormRoutes", async () => {
    renderRail();
    expect(screen.getByTestId("app-rail-section-Settings")).toBeInTheDocument();
    // One entry per settings singleton, routing to its typed form.
    expect(screen.getByTestId("app-rail-item-Orchestrator")).toHaveAttribute(
      "href",
      "/config/settings/orchestrator",
    );
    expect(screen.getByTestId("app-rail-item-Pipeline cost cap")).toHaveAttribute(
      "href",
      "/config/settings/pipeline_cost_cap",
    );
  });

  it("AppRail_SettingsRoute_MarksTheActiveSettingsItem", async () => {
    usePathname.mockReturnValue("/config/settings/orchestrator");
    renderRail();
    expect(screen.getByTestId("rail-toggle-config")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("app-rail-item-Orchestrator")).toHaveAttribute("data-active", "true");
  });

  it("AppRail_ConfigRoute_HistoryShowsChangesCount", async () => {
    renderRail();
    expect(screen.getByTestId("app-rail-section-History")).toBeInTheDocument();
    const changes = await screen.findByTestId("app-rail-count-Changes");
    expect(changes).toHaveTextContent("2");
    expect(screen.getByTestId("app-rail-item-Changes")).toHaveAttribute("href", "/config/changes");
  });

  it("AppRail_ConfigSubroute_TogglesConfigActive_AndMarksSection", async () => {
    usePathname.mockReturnValue("/config/projects");
    renderRail();
    expect(screen.getByTestId("rail-toggle-config")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("rail-toggle-runs")).toHaveAttribute("data-active", "false");
    expect(await screen.findByTestId("app-rail-item-Projects")).toHaveAttribute("data-active", "true");
    expect(screen.getByTestId("app-rail-item-Agents")).toHaveAttribute("data-active", "false");
  });
});
