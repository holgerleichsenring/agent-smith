import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { webcrypto } from "node:crypto";
import { UserManager } from "oidc-client-ts";
import { AccessTokenStore } from "../AccessTokenStore";
import { AuthSession } from "../AuthSession";
import { authoritySettings } from "../createAuthorityClient";
import type { RuntimeAuthSettings } from "@/lib/runtimeSettings/runtimeSettings";

// 2026-08-25-2de1: these cases drive the REAL oidc-client-ts against a stubbed
// authority — the redirect it builds, the code it exchanges, and the end-session
// endpoint it does or does not find. A hand-written stand-in for the client would
// only have proven that the stand-in works.

const ORIGIN = "http://localhost:3000";
const AUTHORITY = "https://login.example.com/realms/sample";
const CLIENT_ID = "dashboard";
const ACCESS_TOKEN = "at-from-the-authority";

const SETTINGS: RuntimeAuthSettings = {
  authority: AUTHORITY,
  clientId: CLIENT_ID,
  audience: "",
  scopes: "openid profile",
  redirectPath: "/signin-callback",
};

/** Captures where the client would have sent the browser. */
class CapturingNavigator {
  readonly urls: string[] = [];

  async prepare(): Promise<unknown> {
    return {
      navigate: async (params: { url: string }) => {
        this.urls.push(params.url);
        return { url: params.url };
      },
      close: () => {},
    };
  }

  async callback(): Promise<void> {}
}

/** Unsigned, because the code flow authenticates the token endpoint by TLS and
 *  the client never verifies a signature here. */
function jwt(claims: Record<string, unknown>): string {
  const part = (value: unknown) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return `${part({ alg: "none", typ: "JWT" })}.${part(claims)}.`;
}

function metadata(endSession: boolean): Record<string, unknown> {
  return {
    issuer: AUTHORITY,
    authorization_endpoint: `${AUTHORITY}/auth`,
    token_endpoint: `${AUTHORITY}/token`,
    ...(endSession ? { end_session_endpoint: `${AUTHORITY}/logout` } : {}),
  };
}

function json(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    headers: { get: () => "application/json" },
    json: async () => body,
    text: async () => JSON.stringify(body),
  } as unknown as Response;
}

/** The token endpoint's answer for the sign-in request that was just built —
 *  the nonce has to be the one the client asked with. */
function grantFor(navigator: CapturingNavigator): Record<string, unknown> {
  const now = Math.floor(Date.now() / 1000);
  return {
    access_token: ACCESS_TOKEN,
    token_type: "Bearer",
    expires_in: 3600,
    scope: "openid profile",
    id_token: jwt({
      iss: AUTHORITY,
      aud: CLIENT_ID,
      sub: "a-person",
      iat: now,
      exp: now + 3600,
      nonce: new URL(navigator.urls[0]).searchParams.get("nonce"),
    }),
  };
}

/** The authority answers discovery, and whatever the test last put on the token
 *  endpoint for a POST. */
function stubAuthority(endSession = true) {
  const state = { grant: {} as unknown };
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: unknown, init?: { method?: string }) => {
      const url = String(input);
      if (url.includes("openid-configuration")) return json(metadata(endSession));
      if (init?.method === "POST") return json(state.grant);
      throw new Error(`the test authority was asked for ${url}`);
    }),
  );
  return state;
}

function clientFor(navigator: CapturingNavigator): UserManager {
  return new UserManager(authoritySettings(SETTINGS, ORIGIN), navigator as unknown as never);
}

/** Runs a real sign-in redirect and hands back the URL the authority would
 *  return to, so the code exchange is driven by a genuine PKCE state. */
async function redirectedBack(
  session: AuthSession,
  navigator: CapturingNavigator,
  query: (state: string) => string,
): Promise<string> {
  await session.signIn("/jobs");
  const state = new URL(navigator.urls[0]).searchParams.get("state") ?? "";
  return `${ORIGIN}/signin-callback?${query(state)}`;
}

beforeEach(() => {
  if (!globalThis.crypto?.subtle) vi.stubGlobal("crypto", webcrypto);
  vi.spyOn(console, "warn").mockImplementation(() => {});
  vi.spyOn(console, "debug").mockImplementation(() => {});
  window.sessionStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("AuthSession.signIn", () => {
  it("SignIn_AuthorityConfigured_RedirectsWithACodeChallenge", async () => {
    stubAuthority();
    const navigator = new CapturingNavigator();
    const session = new AuthSession(clientFor(navigator), new AccessTokenStore());

    await session.signIn("/jobs");

    const url = new URL(navigator.urls[0]);
    expect(`${url.origin}${url.pathname}`).toBe(`${AUTHORITY}/auth`);
    expect(url.searchParams.get("response_type")).toBe("code");
    expect(url.searchParams.get("client_id")).toBe(CLIENT_ID);
    expect(url.searchParams.get("redirect_uri")).toBe(`${ORIGIN}/signin-callback`);
    expect(url.searchParams.get("code_challenge_method")).toBe("S256");
    expect(url.searchParams.get("code_challenge")).toBeTruthy();
  });
});

describe("AuthSession.complete", () => {
  it("Callback_WithACode_ExchangesItAndFillsTheStore", async () => {
    const authority = stubAuthority();
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    const session = new AuthSession(clientFor(navigator), store);
    const back = await redirectedBack(session, navigator, (s) => `code=the-code&state=${s}`);
    authority.grant = grantFor(navigator);

    await session.complete(back);

    expect(session.error).toBeNull();
    expect(store.read()).toBe(ACCESS_TOKEN);
    // The person is put back where the redirect took them from.
    expect(session.returnTo).toBe("/jobs");
  });

  it("Callback_WithAnError_SurfacesItAndLeavesTheStoreEmpty", async () => {
    stubAuthority();
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    const session = new AuthSession(clientFor(navigator), store);
    const back = await redirectedBack(session, navigator, (s) => `error=access_denied&state=${s}`);

    await session.complete(back);

    expect(store.read()).toBeNull();
    expect(session.error).toContain("access_denied");
  });
});

describe("AuthSession.restore", () => {
  it("Restore_NoDirectorySession_LeavesTheStoreEmptyAndNothingThrows", async () => {
    stubAuthority();
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    const client = clientFor(navigator);
    // No directory session for this browser: the silent attempt is refused, and
    // being signed out is a state rather than a fault.
    vi.spyOn(client, "signinSilent").mockRejectedValue(new Error("login_required"));
    const session = new AuthSession(client, store);

    await session.restore();

    expect(store.read()).toBeNull();
  });

  it("Restore_ADirectorySessionExists_FillsTheStoreWithoutARedirect", async () => {
    stubAuthority();
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    const client = clientFor(navigator);
    vi.spyOn(client, "signinSilent").mockResolvedValue({
      access_token: ACCESS_TOKEN,
      expires_at: Math.floor(Date.now() / 1000) + 3600,
    } as never);
    const session = new AuthSession(client, store);

    await session.restore();

    expect(store.read()).toBe(ACCESS_TOKEN);
    expect(navigator.urls).toEqual([]);
  });
});

describe("AuthSession.signOut", () => {
  it("SignOut_ClearsTheStore", async () => {
    stubAuthority();
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    store.hold({ accessToken: ACCESS_TOKEN, expiresAt: Date.now() + 3_600_000 });
    const session = new AuthSession(clientFor(navigator), store);

    await session.signOut();

    expect(store.read()).toBeNull();
  });

  it("SignOut_AuthorityPublishesAnEndSession_RedirectsThroughIt", async () => {
    stubAuthority(true);
    const navigator = new CapturingNavigator();
    const session = new AuthSession(clientFor(navigator), new AccessTokenStore());

    await session.signOut();

    const url = new URL(navigator.urls[0]);
    expect(`${url.origin}${url.pathname}`).toBe(`${AUTHORITY}/logout`);
  });

  it("SignOut_AuthorityPublishesNone_StillEndsTheLocalSession", async () => {
    stubAuthority(false);
    const navigator = new CapturingNavigator();
    const store = new AccessTokenStore();
    store.hold({ accessToken: ACCESS_TOKEN, expiresAt: Date.now() + 3_600_000 });
    const session = new AuthSession(clientFor(navigator), store);

    await session.signOut();

    expect(store.read()).toBeNull();
    expect(navigator.urls).toEqual([]);
  });
});
