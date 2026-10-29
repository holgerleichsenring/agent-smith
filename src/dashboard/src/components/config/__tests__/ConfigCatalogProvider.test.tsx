import { describe, it, expect, vi } from "vitest";
import { act, render, screen, waitFor } from "@testing-library/react";
import { ConfigCatalogProvider, useConfigCatalogContext } from "@/components/config/ConfigCatalogProvider";
import { trackersApi } from "@/lib/configApi";

// 2026-10-29-6bd3b: a tracker created in another tab was never offered by a project form left
// open here. The catalog is re-read, without the loading state, when the tab is visible again.

vi.mock("@/lib/configApi", () => {
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  });
  return {
    agentsApi: client([]),
    trackersApi: client([{ id: "first" }]),
    connectionsApi: client([]),
    reposApi: client([]),
    projectsApi: client([]),
    mcpServersApi: client([]),
    designSourcesApi: client([]),
    secretsApi: client([]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    fetchInheritedSandbox: vi.fn().mockResolvedValue(null),
  };
});

const seen: boolean[] = [];

function Probe() {
  const { catalog, loading } = useConfigCatalogContext();
  seen.push(loading);
  return <div data-testid="trackers">{catalog.trackers.map((t) => t.id).join(",")}</div>;
}

function becomeVisible(state: DocumentVisibilityState) {
  Object.defineProperty(document, "visibilityState", { configurable: true, value: state });
  document.dispatchEvent(new Event("visibilitychange"));
}

describe("ConfigCatalogProvider", () => {
  it("CatalogProvider_ATrackerAddedWhileHidden_IsInTheCatalogOnceVisible_WithoutLoading", async () => {
    render(
      <ConfigCatalogProvider>
        <Probe />
      </ConfigCatalogProvider>,
    );
    await waitFor(() => expect(screen.getByTestId("trackers").textContent).toBe("first"));
    vi.mocked(trackersApi.list).mockResolvedValue([{ id: "first" }, { id: "second" }] as never);
    seen.length = 0;

    act(() => becomeVisible("hidden"));
    expect(vi.mocked(trackersApi.list)).toHaveBeenCalledTimes(1);
    act(() => becomeVisible("visible"));

    await waitFor(() => expect(screen.getByTestId("trackers").textContent).toBe("first,second"));
    expect(seen).not.toContain(true);
  });
});
