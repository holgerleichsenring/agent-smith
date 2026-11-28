import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { placeForPath, PLACES } from "../places";
import { AppSidebar } from "@/components/shell/AppSidebar";
import { ConfigSectionNav } from "@/components/config/ConfigSectionNav";
import { SystemSectionNav } from "@/components/system/SystemSectionNav";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// 2026-08-27-1ed6: one table names every place. 2026-11-16-0440c: the top bar's search goes to a
// place by that name, so every destination the shell links — the sidebar, the profile menu and
// the configuration and system section lists — must be one the table can name.

const usePathname = vi.fn(() => "/");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname(), useRouter: () => ({ push: vi.fn() }) }));

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
vi.mock("@/lib/pullRequestsApi", () => ({ fetchPullRequests: vi.fn().mockResolvedValue([]) }));
// 2026-11-23-933da: PoCs on, so the sidebar links /pocs and the table must name it.
vi.mock("@/lib/pocsApi", () => ({
  fetchPocs: vi.fn().mockResolvedValue({ status: "on", list: { pocs: [], mayImport: false, targets: [] } }),
}));

// The factory is hoisted above imports, so all fixtures live inside it.
vi.mock("@/lib/configApi", () => {
  const client = () => ({
    list: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  });
  return {
    agentsApi: client(),
    trackersApi: client(),
    connectionsApi: client(),
    reposApi: client(),
    projectsApi: client(),
    mcpServersApi: client(),
    designSourcesApi: client(),
    secretsApi: client(),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
  };
});

/** Every href the shell links: the sidebar with its profile menu open, and both section lists. */
function shellHrefs(): string[] {
  usePathname.mockReturnValue("/");
  const view = render(
    <EventStoreProvider store={silentEventStore()}>
      <ConfigCatalogProvider>
        <AppSidebar />
        <ConfigSectionNav />
        <SystemSectionNav />
      </ConfigCatalogProvider>
    </EventStoreProvider>,
  );
  fireEvent.click(screen.getByTestId("profile-button"));
  const hrefs = [...document.querySelectorAll("a[href]")].map((link) => link.getAttribute("href") ?? "");
  view.unmount();
  return hrefs;
}

beforeEach(() => usePathname.mockReturnValue("/"));

describe("Places", () => {
  it("Places_EveryShellHref_ResolvesToAPlace", () => {
    const hrefs = shellHrefs();
    expect(hrefs.length).toBeGreaterThan(30);

    const unnamed = hrefs.filter((href) => placeForPath(href) === null);
    expect(unnamed, `shell destinations the places table cannot name: ${unnamed.join(", ")}`).toEqual([]);
  });

  it("Places_EveryFormerRailDestination_IsStillLinked", () => {
    const hrefs = shellHrefs();
    for (const href of [
      "/runs", "/tickets", "/tickets?bucket=needs-you", "/pull-requests", "/spec-dialog", "/overview", "/delivery",
      "/system/tracker", "/system/webhooks", "/system/chat", "/system/config", "/system/catalog",
      "/config", "/config/projects", "/config/access", "/config/installation", "/config/connection-check",
      "/config/changes", "/config/settings/runs", "/system",
    ]) {
      expect(hrefs, href).toContain(href);
    }
  });

  it("Places_Pocs_IsPocs", () => {
    expect(placeForPath("/pocs")).toBe("PoCs");
    expect(placeForPath("/pocs/import")).toBe("Import a PoC");
  });

  it("Places_Delivery_IsDelivery", () => {
    expect(placeForPath("/delivery")).toBe("Delivery");
  });

  // 2026-10-13-9a59p: a bucket is a query of one place, and a ticket's own page is matched like a run's.
  it("Places_TicketList_AndItsBuckets_AreOnePlace_AndATicketIsAPlace", () => {
    for (const bucket of ["needs-you", "working", "stuck", "ended", "done"]) {
      expect(placeForPath(`/tickets?bucket=${bucket}`)).toBe("Tickets");
    }
    expect(placeForPath("/tickets")).toBe("Tickets");
    expect(placeForPath("/tickets/shop/GH-101")).toBe("Ticket");
    expect(placeForPath("/tickets/shop")).toBeNull();
  });

  // 2026-11-23-31dcd: the series list is a place, and one series' page is matched like a ticket's.
  it("Places_SeriesList_AndASeries_ArePlaces", () => {
    expect(placeForPath("/series")).toBe("Series");
    expect(placeForPath("/series/sr-1")).toBe("Series");
    expect(placeForPath("/series/sr-1/more")).toBeNull();
  });

  it("Places_WorkItOut_IsATopLevelDestination", () => {
    expect(placeForPath("/spec-dialog")).toBe("Work it out");
  });

  it("Places_TheRunsPath_IsOnePlaceNotFiveBuckets", () => {
    // The five run buckets differ only by a query the root layout may not read, so
    // they are one place — and the table holds one row for them, not five.
    for (const bucket of ["all", "needs-you", "running", "queued", "finished"]) {
      const href = bucket === "all" ? "/runs" : `/runs?bucket=${bucket}`;
      expect(placeForPath(href)).toBe("Runs");
    }
    expect(Object.keys(PLACES).filter((path) => path.includes("?"))).toEqual([]);
  });

  // 2026-11-17-d6fba: / is the input page.
  it("Places_TheRoot_IsHome", () => {
    expect(placeForPath("/")).toBe("Home");
  });

  it("Places_ARunUrl_IsNamedWithoutBeingListed", () => {
    // A run id is minted per run, so the two run views are matched, not tabulated.
    expect(placeForPath("/jobs/9f3c-1b2a")).toBe("Run");
    expect(placeForPath("/jobs/9f3c-1b2a/why")).toBe("Run · why");
    expect(placeForPath("/jobs")).toBeNull();
  });

  it("Places_APathTheTableDoesNotHold_IsNoPlace", () => {
    expect(placeForPath("/something-nobody-named")).toBeNull();
    expect(placeForPath(null)).toBe("Home");
  });

  it("Places_TheConfigSubtree_IsDerivedFromTheCatalogAndSettings", () => {
    // Derived, not duplicated: a renamed entity kind or settings key renames its place.
    expect(placeForPath("/config/repos")).toBe("Repositories");
    expect(placeForPath("/config/settings/pipeline_cost_cap")).toBe("Pipeline cost cap");
    // 2026-11-28-9bd1: the groups are places, and a bare /config/settings is the first of them.
    expect(placeForPath("/config/settings/runs")).toBe("Runs & limits");
    expect(placeForPath("/config/settings/workflow")).toBe("Workflow");
    expect(placeForPath("/config/settings/diagnostics")).toBe("Diagnostics");
    expect(placeForPath("/config/settings")).toBe("Runs & limits");
    expect(placeForPath("/config/settings/tickets")).toBe("Ticket limits");
    expect(placeForPath("/config/access")).toBe("Permissions");
    expect(placeForPath("/config/connection-check")).toBe("Connection check");
  });
});
