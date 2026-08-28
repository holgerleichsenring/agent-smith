import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { placeForPath, PLACES } from "../places";
import { AppRail } from "@/components/shell/AppRail";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

// 2026-08-27-1ed6: the header names the place from one table, and the rail is what walks
// the operator to those places. A rail destination the table cannot name would render a
// header with a blank left half, so the two are asserted against each other rather than
// kept in step by hand.

const usePathname = vi.fn(() => "/");
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
vi.mock("@/lib/pullRequestsApi", () => ({ fetchPullRequests: vi.fn().mockResolvedValue([]) }));

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
    secretsApi: client(),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
  };
});

function railHrefs(pathname: string): string[] {
  usePathname.mockReturnValue(pathname);
  const view = render(
    <EventStoreProvider store={silentEventStore()}>
      <ConfigCatalogProvider>
        <AppRail />
      </ConfigCatalogProvider>
    </EventStoreProvider>,
  );
  const links = [...screen.getByTestId("app-rail").querySelectorAll("a[href]")];
  const hrefs = links.map((link) => link.getAttribute("href") ?? "");
  view.unmount();
  return hrefs;
}

beforeEach(() => usePathname.mockReturnValue("/"));

describe("Places", () => {
  it("Places_EveryRailHref_ResolvesToAPlace", () => {
    // Both rail modes — the runs rail and the configuration rail are one component
    // showing two sets of destinations.
    const hrefs = [...railHrefs("/"), ...railHrefs("/config")];
    expect(hrefs.length).toBeGreaterThan(20);

    const unnamed = hrefs.filter((href) => placeForPath(href) === null);
    expect(unnamed, `rail destinations the places table cannot name: ${unnamed.join(", ")}`)
      .toEqual([]);
  });

  it("Places_TheHomePath_IsOnePlaceNotFiveBuckets", () => {
    // The five monitor entries differ only by a query the root layout may not read, so
    // they are one place — and the table holds one row for them, not five.
    for (const bucket of ["all", "needs-you", "running", "queued", "finished"]) {
      const href = bucket === "all" ? "/" : `/?bucket=${bucket}`;
      expect(placeForPath(href)).toBe("Runs");
    }
    expect(Object.keys(PLACES).filter((path) => path.startsWith("/?"))).toEqual([]);
  });

  it("Places_ARunUrl_IsNamedWithoutBeingListed", () => {
    // A run id is minted per run, so the two run views are matched, not tabulated.
    expect(placeForPath("/jobs/9f3c-1b2a")).toBe("Run");
    expect(placeForPath("/jobs/9f3c-1b2a/why")).toBe("Run · why");
    expect(placeForPath("/jobs")).toBeNull();
  });

  it("Places_APathTheTableDoesNotHold_IsNoPlace", () => {
    expect(placeForPath("/something-nobody-named")).toBeNull();
    expect(placeForPath(null)).toBe("Runs");
  });

  it("Places_TheConfigSubtree_IsDerivedFromTheCatalogAndSettings", () => {
    // Derived, not duplicated: a renamed entity kind or settings key renames its place.
    expect(placeForPath("/config/repos")).toBe("Repositories");
    expect(placeForPath("/config/settings/pipeline_cost_cap")).toBe("Pipeline cost cap");
    expect(placeForPath("/config/access")).toBe("Permissions");
    expect(placeForPath("/config/connection-check")).toBe("Connection check");
  });
});
