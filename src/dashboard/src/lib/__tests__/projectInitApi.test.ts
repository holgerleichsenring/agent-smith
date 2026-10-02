import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchProjectInit } from "../projectInitApi";

// 2026-10-02-5f89d: the read the Initialize button restores itself from.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function response(body: unknown, status: number): Response {
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

describe("fetchProjectInit", () => {
  it("fetchProjectInit_ALiveRun_ReadsItsIdAndState", async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({ runId: "r1", state: "cancelling" }, 200));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchProjectInit("my project")).resolves.toEqual({ runId: "r1", state: "cancelling" });
    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/projects/my%20project/init");
  });

  it("fetchProjectInit_NoContent_IsNull", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response(null, 204)));

    await expect(fetchProjectInit("sample")).resolves.toBeNull();
  });

  it("fetchProjectInit_Refused_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response({}, 403)));

    await expect(fetchProjectInit("sample")).rejects.toBeTruthy();
  });
});
