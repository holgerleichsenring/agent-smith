import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "../apiResponse";
import { fetchPocs, importPocUpload, uploadPocZip } from "../pocsApi";

// 2026-11-23-933da: GET /api/pocs is the feature switch too — 200 is the list, 404 off, 403 not permitted.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function plain(status: number, text: string): Response {
  return {
    ok: false, status, text: async () => text, json: async () => { throw new Error("not json"); },
    headers: { get: () => "text/plain" },
  } as unknown as Response;
}

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

// 2026-11-23-933db: the upload is one multipart post (the file, and the target when one is picked); the import is a
// JSON post naming the choice. A refusal is the server's plain sentence, carried whole as the reason.

const UPLOAD = {
  uploadId: "u-1", fileName: "shift-planner.zip", target: "main", port: 8000, entryCount: 12, unpackedBytes: 2048,
  proposedName: "shift-planner", checks: ["paths"], match: null,
};

describe("uploadPocZip", () => {
  it("uploadPocZip_PostsTheFileAndTheTarget_AndReadsTheAnswer", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, UPLOAD));
    vi.stubGlobal("fetch", fetchMock);

    await expect(uploadPocZip(new File(["zip"], "shift-planner.zip"), "main")).resolves.toEqual(UPLOAD);

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/uploads$/);
    expect(init.method).toBe("POST");
    const form = init.body as FormData;
    expect((form.get("file") as File).name).toBe("shift-planner.zip");
    expect(form.get("target")).toBe("main");
  });

  it("uploadPocZip_WithoutTarget_SendsNone", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, UPLOAD));
    vi.stubGlobal("fetch", fetchMock);

    await uploadPocZip(new File(["zip"], "a.zip"));

    expect((fetchMock.mock.calls[0][1].body as FormData).has("target")).toBe(false);
  });

  it("uploadPocZip_422_ThrowsTheSentenceWhole", async () => {
    const sentence = "'app/.env' is not allowed in a PoC (pocs.archive forbidden names).";
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(422, sentence)));

    const thrown = await uploadPocZip(new File(["zip"], "a.zip")).catch((err: unknown) => err);

    expect(thrown).toBeInstanceOf(ApiResponseError);
    expect((thrown as ApiResponseError).status).toBe(422);
    expect((thrown as ApiResponseError).reason).toBe(sentence);
  });
});

describe("importPocUpload", () => {
  it("importPocUpload_PostsTheChoiceAsJson_AndReadsTheRun", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, revision: 2, runId: "r-7" }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(importPocUpload("u 1", { choice: "replace", pocId: 4, confirmed: true }))
      .resolves.toEqual({ pocId: 4, revision: 2, runId: "r-7" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/uploads\/u%201\/import$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ choice: "replace", pocId: 4, confirmed: true });
  });

  it("importPocUpload_404Empty_ThrowsWithNoReason", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(404, "")));

    const thrown = await importPocUpload("u-1", { choice: "new", name: "a" }).catch((err: unknown) => err);

    expect((thrown as ApiResponseError).status).toBe(404);
    expect((thrown as ApiResponseError).reason).toBeNull();
  });
});
