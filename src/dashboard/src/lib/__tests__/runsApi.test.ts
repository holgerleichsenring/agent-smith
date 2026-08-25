import { describe, it, expect, vi, afterEach } from "vitest";
import { fetchRuns, fetchRun } from "../runsApi";

// 2026-08-25-39ab: the run list is the first thing the dashboard reads. A body
// that answers without one of its halves used to reach the hook as `undefined`
// and be counted — the list read `r.active` and `r.recent` unguarded.

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchRuns", () => {
  it("fetchRuns_BothHalves_ReadsThem", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(jsonResponse({ active: [{ runId: "a" }], recent: [] })),
    );

    await expect(fetchRuns()).resolves.toEqual({ active: [{ runId: "a" }], recent: [] });
  });

  it("fetchRuns_AHalfTheServerDidNotSend_ReadsAsEmpty", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse({ recent: [{ runId: "b" }] })));

    await expect(fetchRuns()).resolves.toEqual({ active: [], recent: [{ runId: "b" }] });
  });
});

describe("fetchRun", () => {
  it("fetchRun_NoSuchRun_IsNullNotAnError", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(null, 404)));

    await expect(fetchRun("nope")).resolves.toBeNull();
  });

  it("fetchRun_ABodyThatIsNotJson_FailsWithAReadableMessage", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.reject(new SyntaxError("Unexpected token '<'")),
        headers: { get: () => "text/html" },
      } as unknown as Response),
    );

    await expect(fetchRun("r1")).rejects.toThrow(/cannot read as JSON/);
  });
});
