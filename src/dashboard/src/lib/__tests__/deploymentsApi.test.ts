import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchDeploymentsPage, listDeploymentBindings } from "../deploymentsApi";
import { windowHref, windowOf } from "@/components/deployments/deploymentsWindow";

// 2026-11-23-8df6a: the Deployments page reads /api/deployments with its span; the deploy act's binding read keeps its
// route under its new name. The span in the address is 7, 30 or 90, the default left out.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function json(body: unknown): Response {
  return {
    ok: true, status: 200, text: async () => JSON.stringify(body), json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("deploymentsApi", () => {
  it("fetchDeploymentsPage_ReadsTheSpan_ListDeploymentBindings_ReadsTheProjectsBindings", async () => {
    const fetchMock = vi.fn().mockResolvedValue(json({ merged: [], deploys: [] }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchDeploymentsPage(30)).resolves.toEqual({ merged: [], deploys: [] });
    await listDeploymentBindings("team/alpha");

    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/deployments?days=30");
    expect(String(fetchMock.mock.calls[1][0])).toContain("/api/projects/team%2Falpha/deployments");
  });

  it("deploymentsWindow_KnownSpansOnly_DefaultLeftOutOfTheAddress", () => {
    expect([windowOf("30"), windowOf("90"), windowOf("12"), windowOf(null)]).toEqual([30, 90, 7, 7]);
    expect(windowHref("/deployments", 7)).toBe("/deployments");
    expect(windowHref("/deployments", 90)).toBe("/deployments?days=90");
  });
});
