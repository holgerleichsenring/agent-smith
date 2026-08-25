import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// 2026-08-25-8c97: the bundle names the build it came from on the findings request, and
// only there. BUILD_REVISION is inlined by Next.js at build time, so the module reads it
// once at import — which is why each case re-imports after setting the environment.
describe("fetchFindings", () => {
  const fetchMock = vi.fn();
  const original = process.env.NEXT_PUBLIC_BUILD_REVISION;

  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ degraded: false, blocking: 0, advisory: 0, findings: [] }),
    });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    process.env.NEXT_PUBLIC_BUILD_REVISION = original;
    vi.unstubAllGlobals();
  });

  it("names the build this bundle came from", async () => {
    process.env.NEXT_PUBLIC_BUILD_REVISION = "abc123";

    const { fetchFindings } = await import("@/lib/findingsApi");
    await fetchFindings();

    // 2026-08-25-2de1: the settings document is read before the first API call,
    // so the findings request is no longer the only thing this spy saw.
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/config/findings?build=abc123",
      expect.anything(),
    );
  });

  it("names no build when the bundle was never stamped", async () => {
    delete process.env.NEXT_PUBLIC_BUILD_REVISION;

    const { fetchFindings } = await import("@/lib/findingsApi");
    await fetchFindings();

    expect(fetchMock).toHaveBeenCalledWith("/api/config/findings", expect.anything());
  });
});
