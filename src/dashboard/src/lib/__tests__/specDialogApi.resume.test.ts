import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// 2026-09-22-2a86: the page stopped posting "/spec resume <id>" as message text, so what the
// resume IS now lives in this call: the conversation addressed by its session id, the tab it is
// moved onto in the body. The surface's own tests mock this module, so the address it writes is
// proven here or nowhere.
describe("resumeSpecDialogConversation", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({ ok: true, status: 204, json: async () => ({}) });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("posts the target dialog to the conversation's own resume route", async () => {
    const { resumeSpecDialogConversation } = await import("@/lib/specDialogApi");

    await resumeSpecDialogConversation("s 9/a", "d-fresh");

    const [path, init] = fetchMock.mock.calls.at(-1)!;
    expect(path).toBe("/api/spec-dialog/conversations/s%209%2Fa/resume");
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ dialogId: "d-fresh" });
  });

  // A refused resume — a turn running there, or a dialog that is not the caller's — is an error
  // the surface shows. Swallowed, the page would clear itself and sit on a tab holding nothing.
  it("throws when the route refuses", async () => {
    fetchMock.mockResolvedValue({
      ok: false, status: 409, headers: new Headers(),
      text: async () => "This conversation is in the middle of a turn.",
    });
    const { resumeSpecDialogConversation } = await import("@/lib/specDialogApi");

    await expect(resumeSpecDialogConversation("s-9", "d-fresh")).rejects.toThrow();
  });
});
