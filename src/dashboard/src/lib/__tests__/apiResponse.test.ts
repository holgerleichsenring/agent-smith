import { describe, it, expect, vi, afterEach } from "vitest";
import {
  ApiResponseError,
  apiFetch,
  apiUrl,
  getJson,
  readJson,
  sendJson,
} from "../apiResponse";

// 2026-08-25-39ab: fifteen modules used to compose their own base URL and assert
// their own body with a bare `as`. These cases pin what the one reader owes its
// callers — a message that names the surface, and a single place to change when
// the shape does.

function response(init: {
  ok?: boolean;
  status?: number;
  json?: () => Promise<unknown>;
  contentType?: string;
}): Response {
  return {
    ok: init.ok ?? true,
    status: init.status ?? 200,
    json: init.json ?? (async () => ({})),
    headers: { get: () => init.contentType ?? null },
  } as unknown as Response;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("apiUrl", () => {
  it("apiUrl_NoConfiguredOrigin_ComposesASameOriginPath", () => {
    expect(apiUrl("/api/runs")).toBe("/api/runs");
  });
});

describe("readJson", () => {
  it("ResponseReader_AShapeItDoesNotExpect_FailsInOnePlaceWithAReadableMessage", async () => {
    // A proxy's HTML error page is a 200 the body of which is not JSON — the
    // exact shape that used to surface as an opaque SyntaxError from whichever
    // of the fifteen modules happened to ask.
    const res = response({
      contentType: "text/html",
      json: () => Promise.reject(new SyntaxError("Unexpected token '<'")),
    });

    const failure = await readJson(res, "/api/runs").catch((e: unknown) => e);

    expect(failure).toBeInstanceOf(ApiResponseError);
    const error = failure as ApiResponseError;
    expect(error.path).toBe("/api/runs");
    expect(error.message).toContain("/api/runs");
    expect(error.message).toContain("cannot read as JSON");
    expect(error.message).toContain("text/html");
    expect(error.message).toContain("Unexpected token");
  });

  it("ResponseReader_TheServerRefused_NamesThePathAndTheStatus", async () => {
    const failure = await readJson(response({ ok: false, status: 503 }), "/api/config").catch(
      (e: unknown) => e,
    );

    expect(failure).toBeInstanceOf(ApiResponseError);
    expect((failure as ApiResponseError).status).toBe(503);
    expect((failure as Error).message).toBe("/api/config: HTTP 503");
  });

  it("ResponseReader_AGoodBody_ReturnsIt", async () => {
    const body = await readJson<{ recent: string[] }>(
      response({ json: async () => ({ recent: ["r1"] }) }),
      "/api/runs",
    );

    expect(body.recent).toEqual(["r1"]);
  });
});

describe("getJson and sendJson", () => {
  it("getJson_APath_ReadsItThroughTheOneReader", async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({ json: async () => ({ ok: true }) }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getJson("/api/pull-requests")).resolves.toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith("/api/pull-requests", expect.anything());
  });

  it("sendJson_ABody_SendsItAsJsonAndReadsTheAnswer", async () => {
    const fetchMock = vi.fn().mockResolvedValue(response({ json: async () => ({ id: "a" }) }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(sendJson("POST", "/api/config/agents", { id: "a" })).resolves.toEqual({
      id: "a",
    });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/config/agents",
      expect.objectContaining({ method: "POST", body: JSON.stringify({ id: "a" }) }),
    );
  });

  it("apiFetch_ARefusalTheCallerBranchesOn_IsHandedBackUnthrown", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response({ ok: false, status: 404 })));

    const res = await apiFetch("/api/runs/nope");

    expect(res.status).toBe(404);
  });
});
