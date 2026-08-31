import { Suspense } from "react";
import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import ConfigPage from "@/app/config/[[...slug]]/page";
import InstallationRedirect from "@/app/system/installation/page";
import ConnectionsRedirect from "@/app/system/connections/page";

// 2026-08-27-1ed6: the installation read-out and the connection check are served from the
// configuration route's slug branch, and their old /system paths lead there. The views
// themselves are unchanged and tested next to themselves — what is asserted here is where
// they now live and that the old links still land.

const redirect = vi.hoisted(() => vi.fn());
vi.mock("next/navigation", () => ({ redirect }));
vi.mock("@/components/system/InstallationIdentityView", () => ({
  InstallationIdentityView: () => <div data-testid="installation-view" />,
}));
vi.mock("@/components/system/ConnectionsView", () => ({
  ConnectionsView: () => <div data-testid="connections-view" />,
}));
vi.mock("@/components/config/ConfigStudio", () => ({ ConfigStudio: () => <div data-testid="studio" /> }));
vi.mock("@/components/config/SettingsStudio", () => ({ SettingsStudio: () => <div data-testid="settings" /> }));
vi.mock("@/components/access/AccessStudio", () => ({ AccessStudio: () => <div data-testid="access" /> }));

// React.use() unwraps an already-instrumented promise synchronously — no Suspense
// round-trip needed in jsdom.
function resolvedParams(slug: string[]): Promise<{ slug?: string[] }> {
  const params = Promise.resolve({ slug });
  Object.assign(params, { status: "fulfilled", value: { slug } });
  return params;
}

const renderConfig = (slug: string[]) =>
  render(
    <Suspense fallback={null}>
      <ConfigPage params={resolvedParams(slug)} />
    </Suspense>,
  );

describe("Configuration route", () => {
  it("Route_TheDiagnosticSlugs_ServeTheMovedReadOuts", async () => {
    renderConfig(["installation"]);
    expect(await screen.findByTestId("installation-view")).toBeInTheDocument();
    // The same parity page scope they rendered inside under /system.
    expect(screen.getByTestId("diagnostic-page").className).toContain("mock-system");
    // A diagnostic page is read at the width of the other /config pages: its cards put
    // the value hard right, so the subsystem streams' 1500px strands it off screen.
    expect(screen.getByTestId("diagnostic-page").className).toContain("mock-diagnostic");

    renderConfig(["connection-check"]);
    expect(await screen.findByTestId("connections-view")).toBeInTheDocument();
  });

  it("Route_TheConnectionCatalog_KeepsItsOwnPath", async () => {
    // /config/connections lists the connection ENTITIES — a different question about the
    // same word than "do the connections answer".
    renderConfig(["connections"]);
    expect(await screen.findByTestId("studio")).toBeInTheDocument();
    expect(screen.queryByTestId("connections-view")).toBeNull();
  });

  it("Route_TheOldInstallationAndConnectionPaths_LandOnTheirNewHomes", () => {
    InstallationRedirect();
    expect(redirect).toHaveBeenCalledWith("/config/installation");

    ConnectionsRedirect();
    expect(redirect).toHaveBeenCalledWith("/config/connection-check");
  });
});
