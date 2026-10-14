import { afterEach, describe, expect, it, vi } from "vitest";
import { postDefaultVerdicts } from "../ticketsApi";

// 2026-10-13-8762q: a refused verdict batch is an answer — the server's JSON-string body, shown as written.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function response(body: unknown, status: number): Response {
  return {
    ok: status >= 200 && status < 300, status, json: async () => body, text: async () => JSON.stringify(body),
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("postDefaultVerdicts", () => {
  it("postDefaultVerdicts_400_ReturnsTheJsonStringAsRefused", async () => {
    const text = "item 1 (default #7): answer it in the design conversation\nNothing was written.";
    const fetchMock = vi.fn().mockResolvedValue(response(text, 400));
    vi.stubGlobal("fetch", fetchMock);

    await expect(postDefaultVerdicts("shop", "101", [{ id: 7, verdict: "overruled", note: "no" }]))
      .resolves.toEqual({ kind: "refused", text });
    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/tickets/shop/101/defaults/verdicts");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual([{ id: 7, verdict: "overruled", note: "no" }]);
  });

  it("postDefaultVerdicts_200_ReturnsAppliedAndAlreadyDecided", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response({ applied: [1], alreadyDecided: [{ id: 2, verdict: "ratified" }] }, 200)));

    await expect(postDefaultVerdicts("shop", "101", [{ id: 1, verdict: "ratified" }, { id: 2, verdict: "ratified" }]))
      .resolves.toEqual({ kind: "applied", applied: [1], alreadyDecided: [{ id: 2, verdict: "ratified" }] });
  });

  it("postDefaultVerdicts_OtherRefusal_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response({}, 404)));

    await expect(postDefaultVerdicts("shop", "101", [{ id: 1, verdict: "ratified" }])).rejects.toBeTruthy();
  });
});
