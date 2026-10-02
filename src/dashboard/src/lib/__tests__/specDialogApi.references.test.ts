import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// 2026-10-01-283db: a website goes up as ONE multipart post, every file under its path inside the
// set — webkitRelativePath for a folder pick, the bare name for a plain one. The surface's tests
// mock this module, so the shape of the post is proven here or nowhere.
describe("uploadSpecDialogReferences", () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({
      ok: true, status: 200,
      json: async () => ({ setId: "s1", name: "site", files: 2, bytes: 2, at: "2026-10-01T10:00:00Z" }),
    });
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => vi.unstubAllGlobals());

  it("Folder appends each file with its webkitRelativePath as the filename", async () => {
    const { uploadSpecDialogReferences } = await import("@/lib/specDialogApi");
    const nested = new File(["a"], "a.css");
    Object.defineProperty(nested, "webkitRelativePath", { value: "site/css/a.css" });
    const plain = new File(["b"], "index.html");

    await uploadSpecDialogReferences("d 1", "sample", [nested, plain]);

    const [path, init] = fetchMock.mock.calls.at(-1)!;
    expect(path).toBe("/api/spec-dialog/references?dialogId=d%201&project=sample");
    expect(init.method).toBe("POST");
    const parts = (init.body as FormData).getAll("file") as File[];
    expect(parts.map((part) => part.name)).toEqual(["site/css/a.css", "index.html"]);
  });

  // 2026-10-02-0d72: the refusal carries the server's reason, which names the file to remove.
  it("throws the server's reason when it refuses the set", async () => {
    fetchMock.mockResolvedValue({
      ok: false, status: 400, headers: new Headers(),
      text: async () => JSON.stringify("'site/LICENSE' is not a file a website is made of"),
    });
    const { uploadSpecDialogReferences } = await import("@/lib/specDialogApi");

    await expect(uploadSpecDialogReferences("d-1", "sample", [new File(["x"], "LICENSE")]))
      .rejects.toThrow("HTTP 400 — 'site/LICENSE' is not a file a website is made of");
  });
});
