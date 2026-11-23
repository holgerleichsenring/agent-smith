import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchSeriesRun, fetchSeriesRunCount, fetchSeriesRuns } from "../seriesRunsApi";

// 2026-11-23-31dcb: the series-run reads address their routes; a project filter rides in the query.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function json(body: unknown): Response {
  return {
    ok: true, status: 200, text: async () => JSON.stringify(body), json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("seriesRunsApi", () => {
  it("fetchSeriesRuns_WithAndWithoutAProject_ReadsTheList", async () => {
    const fetchMock = vi.fn().mockResolvedValue(json([]));
    vi.stubGlobal("fetch", fetchMock);

    await fetchSeriesRuns("team/alpha");
    await fetchSeriesRuns();

    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/series-runs?project=team%2Falpha");
    expect(String(fetchMock.mock.calls[1][0])).toMatch(/\/api\/series-runs$/);
  });

  it("fetchSeriesRunCount_AndFetchSeriesRun_ReadTheirRoutes", async () => {
    const fetchMock = vi.fn().mockResolvedValue(json({ running: 2 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchSeriesRunCount()).resolves.toEqual({ running: 2 });
    await fetchSeriesRun("sr-1");

    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/series-runs/count");
    expect(String(fetchMock.mock.calls[1][0])).toContain("/api/series-runs/sr-1");
  });
});
