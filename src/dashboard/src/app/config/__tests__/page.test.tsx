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
vi.mock("@/components/config/SettingsStudio", () => ({
  SettingsStudio: ({ settingKey }: { settingKey: string }) => <div data-testid={`settings-${settingKey}`} />,
}));
vi.mock("@/components/config/SettingsGroupStudio", () => ({
  SettingsGroupStudio: ({ groupId, open }: { groupId: string; open: string | null }) => (
    <div data-testid={`group-${groupId}`} data-open={open ?? ""} />
  ),
}));
vi.mock("@/components/access/AccessStudio", () => ({ AccessStudio: () => <div data-testid="access" /> }));

// React.use() unwraps an already-instrumented promise synchronously — no Suspense
// round-trip needed in jsdom.
function resolvedParams(slug: string[]): Promise<{ slug?: string[] }> {
  const params = Promise.resolve({ slug });
  Object.assign(params, { status: "fulfilled", value: { slug } });
  return params;
}

function resolved<T>(value: T): Promise<T> {
  const promise = Promise.resolve(value);
  Object.assign(promise, { status: "fulfilled", value });
  return promise;
}

const renderConfig = (slug: string[], search: { open?: string } = {}) =>
  render(
    <Suspense fallback={null}>
      <ConfigPage params={resolvedParams(slug)} searchParams={resolved(search)} />
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

  // 2026-11-28-9bd1: the settings rail lists groups; a former page's path still lands on it.
  it("OldSettingPath_RedirectsToItsGroupWithTheSectionOpen", () => {
    for (const [key, group] of [
      ["orchestrator", "runs"], ["primary_provider", "runs"], ["limits", "runs"], ["pipeline_cost_cap", "runs"],
      ["queue", "runs"], ["deployment", "runs"], ["dialogue", "workflow"], ["spec_review", "workflow"],
      ["preconditions", "workflow"], ["holds", "workflow"], ["branch_cleanup", "workflow"],
      ["pipeline_data_flow", "diagnostics"], ["trace", "diagnostics"],
    ]) {
      redirect.mockClear();
      const view = renderConfig(["settings", key]);
      expect(redirect).toHaveBeenCalledWith(`/config/settings/${group}?open=${key}`);
      view.unmount();
    }
  });

  it("SettingsRoute_AGroup_RendersItWithTheSectionToOpen", async () => {
    renderConfig(["settings", "runs"], { open: "queue" });
    expect(await screen.findByTestId("group-runs")).toHaveAttribute("data-open", "queue");
    renderConfig(["settings", "workflow"], { open: "not-a-key" });
    expect(await screen.findByTestId("group-workflow")).toHaveAttribute("data-open", "");
  });

  it("SettingsRoute_APageOfItsOwn_RendersIt_ABareOrUnknownOne_GoesToTheFirstEntry", async () => {
    redirect.mockClear();
    renderConfig(["settings", "sandbox"]);
    expect(await screen.findByTestId("settings-sandbox")).toBeInTheDocument();
    expect(redirect).not.toHaveBeenCalled();
    renderConfig(["settings"]);
    expect(redirect).toHaveBeenCalledWith("/config/settings/runs");
    redirect.mockClear();
    renderConfig(["settings", "nope"]);
    expect(redirect).toHaveBeenCalledWith("/config/settings/runs");
  });
});
