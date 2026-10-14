import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// 2026-10-13-8762t: a ratification posts the version to the spec's ratify route, and a refusal the server
// words (404, 409) is thrown as that sentence so the strip can show it in place.
describe("ratifySpecMock", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({ ok: true, status: 200, text: async () => "Ratified mock v2 of spec-a." });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("posts the version as JSON to the spec's ratify route", async () => {
    const { ratifySpecMock } = await import("@/lib/specDialogApi");

    await ratifySpecMock("s 1", "spec-a", 2);

    const [path, init] = fetchMock.mock.calls.at(-1)!;
    expect(path).toBe("/api/spec-dialog/conversations/s%201/mocks/spec-a/ratify");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ version: 2 });
  });

  it("throws the server's sentence on 409", async () => {
    fetchMock.mockResolvedValue({
      ok: false, status: 409, headers: new Headers(),
      text: async () => "The design partner is computing a turn; ratify when it has answered.",
    });
    const { ratifySpecMock } = await import("@/lib/specDialogApi");

    await expect(ratifySpecMock("s-1", "spec-a", 1)).rejects.toThrow(
      /^The design partner is computing a turn; ratify when it has answered\.$/);
  });
});
