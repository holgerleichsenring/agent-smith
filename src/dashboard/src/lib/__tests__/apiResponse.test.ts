import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import {
  ApiRefusal,
  ApiResponseError,
  apiFetch,
  apiUrl,
  getJson,
  readJson,
  refusalOf,
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

// 2026-08-25-4530: a refusal is a STATE and a fault is an exception, and the
// single outgoing point is where the two stop being the same thing.
describe("a refusal separated from a fault", () => {
  it("Get_401_ResolvesToASignInStateRatherThanThrowing", async () => {
    const refusal = await refusalOf(response({ ok: false, status: 401 }), "/api/runs");

    expect(refusal?.kind).toBe("sign-in");
    expect(refusal?.status).toBe(401);
    expect(refusal?.message).toContain("signed out");
    // "HTTP 401" is the message that told an operator nothing about the one
    // thing they had to do, and it is gone from this path.
    expect(refusal?.message).not.toContain("HTTP 401");
  });

  it("Get_401_IsNotAnApiResponseError", async () => {
    // Everything that renders a failure branches on this: a 401 is not a fault
    // of the installation, so it must not arrive as one.
    const thrown = await getJsonAgainst({ ok: false, status: 401 }).catch((e: unknown) => e);

    expect(thrown).toBeInstanceOf(ApiRefusal);
    expect(thrown).not.toBeInstanceOf(ApiResponseError);
  });

  it("Get_403_CarriesThePermissionsTheServerNamed", async () => {
    // p0503b replaced the authorization result handler precisely so this body
    // exists — ASP.NET's own forbid path writes nothing at all.
    const refusal = await refusalOf(
      response({
        ok: false,
        status: 403,
        json: async () => ({
          error: "The caller is missing one or more permissions this route requires.",
          missingPermissions: ["config.write", "secrets.read"],
        }),
      }),
      "/api/config/secrets",
    );

    expect(refusal?.kind).toBe("permission");
    expect(refusal?.missingPermissions).toEqual(["config.write", "secrets.read"]);
    expect(refusal?.message).toContain("config.write");
    expect(refusal?.message).toContain("secrets.read");
  });

  it("Get_403_WithAnUnreadableBody_StillNamesTheRefusal", async () => {
    // A proxy's own 403 page carries no such body. The refusal is still a
    // refusal; it just names less.
    const refusal = await refusalOf(
      response({
        ok: false,
        status: 403,
        contentType: "text/html",
        json: () => Promise.reject(new SyntaxError("Unexpected token '<'")),
      }),
      "/api/config/secrets",
    );

    expect(refusal?.kind).toBe("permission");
    expect(refusal?.missingPermissions).toEqual([]);
    expect(refusal?.message).toContain("named no permission");
  });

  it("Get_500_StillThrowsAsItDoesToday", async () => {
    expect(await refusalOf(response({ ok: false, status: 500 }), "/api/runs")).toBeNull();

    const thrown = await getJsonAgainst({ ok: false, status: 500 }).catch((e: unknown) => e);

    expect(thrown).toBeInstanceOf(ApiResponseError);
    expect((thrown as Error).message).toBe("/api/runs: HTTP 500");
  });
});

/** One GET against a stubbed response, through the real outgoing point. */
async function getJsonAgainst(init: Parameters<typeof response>[0]): Promise<unknown> {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(response(init)));
  return getJson("/api/runs");
}

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
