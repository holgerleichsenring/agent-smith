import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchPocs } from "../pocsApi";

// 2026-11-23-933da: GET /api/pocs is the feature switch too — 200 is the list, 404 off, 403 not permitted.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function answer(status: number, body: unknown = ""): Response {
  return {
    ok: status >= 200 && status < 300, status, text: async () => JSON.stringify(body), json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("fetchPocs", () => {
  it("fetchPocs_200_IsOnWithTheList", async () => {
    const list = { pocs: [], mayImport: true, targets: [{ name: "main", sourceRepo: "poc-source", root: "pocs" }] };
    const fetchMock = vi.fn().mockResolvedValue(answer(200, list));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchPocs()).resolves.toEqual({ status: "on", list });
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs$/);
  });

  it("fetchPocs_404_IsOff_403_IsForbidden", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(answer(404)).mockResolvedValueOnce(answer(403, {})));

    await expect(fetchPocs()).resolves.toEqual({ status: "off" });
    await expect(fetchPocs()).resolves.toEqual({ status: "forbidden" });
  });

  it("fetchPocs_500_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(answer(500)));

    await expect(fetchPocs()).rejects.toThrow("/api/pocs");
  });
});
