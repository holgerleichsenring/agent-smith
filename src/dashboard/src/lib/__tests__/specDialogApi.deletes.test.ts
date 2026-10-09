import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// 2026-10-08-e8b9h: an upload is removed by its own id on the dialog it belongs to.
describe("deleteSpecDialogReference / deleteSpecDialogImage", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({ ok: true, status: 204 });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("specDialogApi_DeleteReference_UsesSetIdAndDialogId", async () => {
    const { deleteSpecDialogReference, deleteSpecDialogImage } = await import("@/lib/specDialogApi");

    await deleteSpecDialogReference("d 1", "set/1");
    await deleteSpecDialogImage("d 1", 7);

    const calls = fetchMock.mock.calls.filter(([path]) => String(path).includes("/api/"));
    expect(calls[0][0]).toBe("/api/spec-dialog/references/set%2F1?dialogId=d%201");
    expect(calls[0][1].method).toBe("DELETE");
    expect(calls[1][0]).toBe("/api/spec-dialog/images/7?dialogId=d%201");
  });
});
