import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchTicketGraph, fetchTicketRequires, markLandedByHand } from "../ticketGraphApi";

// 2026-10-14-dbcen: the graph and requires reads, and a mark whose every non-200 answer is the server's plain sentence.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function text(body: string, status: number): Response {
  return {
    ok: status >= 200 && status < 300, status, text: async () => body, json: async () => JSON.parse(body),
    headers: { get: () => (status === 200 ? "application/json" : "text/plain; charset=utf-8") },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("ticketGraphApi", () => {
  it("markLandedByHand_200_IsALanding_AndPostsTheNote", async () => {
    const fetchMock = vi.fn().mockResolvedValue(text(JSON.stringify({ kind: "by-hand" }), 200));
    vi.stubGlobal("fetch", fetchMock);

    await expect(markLandedByHand("shop", "41", "shipped")).resolves.toEqual({ kind: "landed" });
    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/tickets/shop/41/landed-by-hand");
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({ note: "shipped" });
  });

  it.each([
    [400, "A note is required: say why this ticket counts as landed."],
    [404, "No ticket 41 on gh."],
    [409, "41 is already marked done by Ada Lovelace at 2026-10-13 10:00:00Z: shipped."],
    [503, "The tracker gh could not be read (unreachable); nothing was recorded."],
  ])("markLandedByHand_%i_IsTheServersSentenceAsIs", async (status, sentence) => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(text(sentence, status)));

    await expect(markLandedByHand("shop", "41", "shipped")).resolves.toEqual({ kind: "said", text: sentence });
  });

  it("markLandedByHand_ANon2xxWithoutASentence_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(text("", 500)));
    await expect(markLandedByHand("shop", "41", "shipped")).rejects.toBeTruthy();
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(text("", 409)));
    await expect(markLandedByHand("shop", "41", "shipped")).rejects.toBeTruthy();
  });

  it("fetchTicketGraph_AndFetchTicketRequires_ReadTheirRoutes", async () => {
    const fetchMock = vi.fn().mockResolvedValue(text(JSON.stringify({ nodes: [], edges: [] }), 200));
    vi.stubGlobal("fetch", fetchMock);

    await fetchTicketGraph("shop");
    await fetchTicketGraph();
    await fetchTicketRequires("shop", "team/41");
    expect(fetchMock.mock.calls.map((c) => String(c[0]))).toEqual([
      expect.stringContaining("/api/tickets/graph?project=shop"),
      expect.stringMatching(/\/api\/tickets\/graph$/),
      expect.stringContaining("/api/tickets/shop/team%2F41/requires"),
    ]);
  });
});
