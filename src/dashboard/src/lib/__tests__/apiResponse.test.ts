import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
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

// 2026-08-25-2de1: apiFetch's contract towards the sign-in loop is exactly "send
// whatever the accessor says, or nothing at all" — the loop itself is pinned by
// its own cases, so it is stubbed here rather than booted.
const auth = vi.hoisted(() => ({ token: null as string | null }));

vi.mock("@/lib/auth/session", () => ({
  currentAccessToken: async () => auth.token,
}));

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

beforeEach(() => {
  auth.token = null;
});

afterEach(() => {
  vi.unstubAllGlobals();
});

/** The RequestInit apiFetch actually sent. */
function sentInit(fetchMock: ReturnType<typeof vi.fn>): RequestInit {
  return fetchMock.mock.calls[0][1] as RequestInit;
}

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

describe("apiFetch and the bearer header", () => {
  it("ApiFetch_StoreEmpty_SendsNoAuthorizationHeader", async () => {
    // An installation with no authority configured sends the request this client
    // has always sent — the caller's init, untouched.
    const fetchMock = vi.fn().mockResolvedValue(response({}));
    vi.stubGlobal("fetch", fetchMock);

    await apiFetch("/api/runs", { method: "GET" });

    expect(sentInit(fetchMock)).toEqual({ method: "GET" });
  });

  it("ApiFetch_StoreHoldsAToken_SendsItAsABearer", async () => {
    auth.token = "at-1";
    const fetchMock = vi.fn().mockResolvedValue(response({}));
    vi.stubGlobal("fetch", fetchMock);

    await apiFetch("/api/runs");

    const headers = new Headers(sentInit(fetchMock).headers);
    expect(headers.get("Authorization")).toBe("Bearer at-1");
  });

  it("ApiFetch_ExistingHeaders_ArePreserved", async () => {
    auth.token = "at-1";
    const fetchMock = vi.fn().mockResolvedValue(response({}));
    vi.stubGlobal("fetch", fetchMock);

    await sendJson("POST", "/api/config/agents", { id: "a" });

    const init = sentInit(fetchMock);
    const headers = new Headers(init.headers);
    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get("Authorization")).toBe("Bearer at-1");
    expect(init.body).toBe(JSON.stringify({ id: "a" }));
  });
});
