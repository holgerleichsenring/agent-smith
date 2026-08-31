import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import type { RuntimeAuthSettings } from "@/lib/runtimeSettings/runtimeSettings";

// 2026-08-25-2de1: the one loop per tab. These cases pin which path a boot takes
// — nothing at all without an authority, a SILENT attempt with one, and the code
// exchange rather than a second sign-in when the authority has just returned.

const AUTHORITY: Partial<RuntimeAuthSettings> = {
  authority: "https://login.example.com/realms/sample",
  clientId: "dashboard",
};

/** A user the authority would hand back, expiring an hour from now. */
function live(overrides: Record<string, unknown> = {}) {
  return {
    access_token: "at-1",
    expires_at: Math.floor(Date.now() / 1000) + 3600,
    expired: false,
    state: { returnTo: "/jobs" },
    ...overrides,
  };
}

/** Only the members AuthSession reaches for. getUser is what this tab already
 *  holds; signinCallback is the dispatching reply route. */
function fakeClient(held: unknown = null) {
  return {
    getUser: vi.fn(async () => held),
    signinSilent: vi.fn(async () => null),
    signinRedirect: vi.fn(async () => undefined),
    signinCallback: vi.fn(async () => live()),
    removeUser: vi.fn(async () => undefined),
    metadataService: { getEndSessionEndpoint: vi.fn(async () => undefined) },
  };
}

// The boot memoises its answer in module state, so every case imports fresh
// rather than reaching for a reset only tests would call.
async function bootWith(auth: Partial<RuntimeAuthSettings>, client: unknown | null) {
  vi.resetModules();
  vi.doMock("@/lib/runtimeSettings/runtimeSettings", async (importOriginal) => {
    const actual = await importOriginal<typeof import("@/lib/runtimeSettings/runtimeSettings")>();
    return {
      ...actual,
      loadRuntimeSettings: async () => ({
        auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, ...auth },
      }),
    };
  });
  const created = vi.fn(() => client);
  vi.doMock("../createAuthorityClient", async (importOriginal) => {
    const actual = await importOriginal<typeof import("../createAuthorityClient")>();
    return { ...actual, createAuthorityClient: created };
  });
  return {
    module: await import("../session"),
    // The same fresh registry, so this IS the store the boot filled.
    store: await import("../AccessTokenStore"),
    created,
  };
}

const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

beforeEach(() => {
  vi.spyOn(console, "debug").mockImplementation(() => {});
  vi.spyOn(console, "warn").mockImplementation(() => {});
  window.history.replaceState({}, "", "/jobs");
});

afterEach(() => {
  vi.doUnmock("@/lib/runtimeSettings/runtimeSettings");
  vi.doUnmock("../createAuthorityClient");
  vi.restoreAllMocks();
});

describe("startAuthSession", () => {
  it("SignIn_NoAuthorityConfigured_IsNeverAttempted", async () => {
    const { module, created } = await bootWith({}, null);

    expect(await module.startAuthSession()).toBeNull();
    expect(await module.currentAccessToken()).toBeNull();
    // The client is asked for, and refuses to exist — no redirect is possible.
    expect(created).toHaveBeenCalledTimes(1);
  });

  it("Boot_AuthorityConfigured_AttemptsASilentSignInAndNeverARedirect", async () => {
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();

    expect(client.signinSilent).toHaveBeenCalledTimes(1);
    expect(client.signinRedirect).not.toHaveBeenCalled();
  });

  it("Boot_ReadTwice_IsTheSameLoop", async () => {
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);

    const [first, second] = await Promise.all([
      module.startAuthSession(),
      module.startAuthSession(),
    ]);

    expect(first).toBe(second);
    expect(client.signinSilent).toHaveBeenCalledTimes(1);
  });

  it("Boot_OnTheCallbackRoute_ExchangesTheCodeInsteadOfSigningInAgain", async () => {
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);

    const session = await module.startAuthSession();

    expect(client.signinCallback).toHaveBeenCalledTimes(1);
    expect(client.signinSilent).not.toHaveBeenCalled();
    expect(await module.currentAccessToken()).toBe("at-1");
    expect(session?.returnTo).toBe("/jobs");
  });

  // An authority whose reply URL is registered without a path returns to "/".
  // Reading every visit to the home page as a callback would exchange a code
  // that is not there, record the refusal and empty the store — leaving the tab
  // signed out on the one route it is most often opened at.
  it("Boot_OnTheRedirectRouteWithNoCode_RestoresInsteadOfExchanging", async () => {
    window.history.replaceState({}, "", "/");
    const client = fakeClient();
    const { module } = await bootWith({ ...AUTHORITY, redirectPath: "/" }, client);

    const session = await module.startAuthSession();

    expect(client.signinCallback).not.toHaveBeenCalled();
    expect(client.signinSilent).toHaveBeenCalledTimes(1);
    expect(session?.error).toBeNull();
  });

  it("Boot_OnAPathlessRedirectCarryingACode_ExchangesIt", async () => {
    window.history.replaceState({}, "", "/?code=the-code&state=the-state");
    const client = fakeClient();
    const { module } = await bootWith({ ...AUTHORITY, redirectPath: "/" }, client);

    await module.startAuthSession();

    expect(client.signinCallback).toHaveBeenCalledTimes(1);
    expect(client.signinSilent).not.toHaveBeenCalled();
  });

  // The authority answers a refusal on the same route, and it is still an
  // arrival: exchanging it is what turns it into a reported error.
  it("Boot_OnTheRedirectRouteCarryingAnError_IsStillAnArrival", async () => {
    window.history.replaceState({}, "", "/?error=access_denied");
    const client = fakeClient();
    const { module } = await bootWith({ ...AUTHORITY, redirectPath: "/" }, client);

    await module.startAuthSession();

    expect(client.signinCallback).toHaveBeenCalledTimes(1);
    expect(client.signinSilent).not.toHaveBeenCalled();
  });

  it("Boot_AfterTheExchange_TheSpentCodeIsGoneFromTheAddressBar", async () => {
    window.history.replaceState({}, "", "/?code=the-code&state=the-state&tab=runs");
    const { module } = await bootWith({ ...AUTHORITY, redirectPath: "/" }, fakeClient());

    await module.startAuthSession();

    expect(window.location.search).toBe("?tab=runs");
  });

  it("Boot_NotOnTheRedirectRoute_LeavesAQueryOfItsOwnAlone", async () => {
    window.history.replaceState({}, "", "/jobs?code=not-an-authorization-code");
    const client = fakeClient();
    const { module } = await bootWith({ ...AUTHORITY, redirectPath: "/" }, client);

    await module.startAuthSession();

    expect(client.signinCallback).not.toHaveBeenCalled();
    expect(window.location.search).toBe("?code=not-an-authorization-code");
  });
});

describe("signIn", () => {
  it("SignIn_NoAuthorityConfigured_DoesNothingAndDoesNotThrow", async () => {
    const { module } = await bootWith({}, null);

    await expect(module.signIn()).resolves.toBeUndefined();
  });

  it("SignIn_AuthorityConfigured_CarriesTheRouteThePersonWasOn", async () => {
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);

    await module.signIn();

    expect(client.signinRedirect).toHaveBeenCalledWith({ state: { returnTo: "/jobs" } });
  });
});

describe("signOut", () => {
  it("SignOut_NoAuthorityConfigured_DoesNothingAndDoesNotThrow", async () => {
    const { module } = await bootWith({}, null);

    await expect(module.signOut()).resolves.toBeUndefined();
  });

  it("SignOut_AuthorityConfigured_EndsTheLocalSession", async () => {
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);
    await module.startAuthSession();

    await module.signOut();

    expect(client.removeUser).toHaveBeenCalledTimes(1);
    expect(await module.currentAccessToken()).toBeNull();
  });
});

// 2026-08-28-0f46: the restore consults what this tab already holds before it
// asks the authority anything. Every load used to pay ten seconds here — the
// silent attempt it awaited could never complete — and both outgoing paths await
// this boot, so the first request and the first hub negotiate paid it too.
describe("restore", () => {
  it("Boot_WithNothingHeld_ReturnsBeforeTheSilentAttemptSettles", async () => {
    let land: (user: unknown) => void = () => {};
    const outstanding = new Promise<unknown>((resolve) => { land = resolve; });
    const client = { ...fakeClient(), signinSilent: vi.fn(() => outstanding) };
    const { module } = await bootWith(AUTHORITY, client);

    const session = await module.startAuthSession();

    // The boot has settled and the attempt has not — which is the whole point.
    expect(session).not.toBeNull();
    expect(client.signinSilent).toHaveBeenCalledTimes(1);
    expect(await module.currentAccessToken()).toBeNull();

    // And the attempt is still adopted when it lands, so the invisible sign-in
    // the earlier phase promised still happens.
    land(live());
    await settle();
    expect(await module.currentAccessToken()).toBe("at-1");
  });

  it("Restore_WithAHeldSession_FindsItWithoutTheAuthorizationEndpoint", async () => {
    const client = fakeClient(live());
    const { module } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();

    expect(client.getUser).toHaveBeenCalledTimes(1);
    expect(client.signinSilent).not.toHaveBeenCalled();
    expect(await module.currentAccessToken()).toBe("at-1");
  });

  it("Restore_ExpiredWithARefreshToken_RenewsAndHoldsTheResult", async () => {
    const held = live({ expired: true, refresh_token: "rt-1", access_token: "at-old" });
    const client = {
      ...fakeClient(held),
      signinSilent: vi.fn(async () => live({ access_token: "at-2" })),
    };
    const { module } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();

    // A refresh token makes this one direct token request, not the hidden frame.
    expect(client.signinSilent).toHaveBeenCalledTimes(1);
    expect(await module.currentAccessToken()).toBe("at-2");
  });

  it("Restore_ExpiredWithoutARefreshToken_ClearsAndReadsSignedOut", async () => {
    const client = fakeClient(live({ expired: true }));
    const { module, store } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();

    // Renewing it would be the hidden frame and its ten seconds, and the token
    // it holds is refused by every call it would be sent on.
    expect(client.signinSilent).not.toHaveBeenCalled();
    expect(client.removeUser).toHaveBeenCalledTimes(1);
    expect(await module.currentAccessToken()).toBeNull();
    expect(store.getAccessTokenStore().state().ended).toBe("expired");
  });

  it("Restore_NothingHeldAndTheAttemptFails_SaysNoSessionEnded", async () => {
    const client = {
      ...fakeClient(),
      signinSilent: vi.fn(async () => { throw new Error("login_required"); }),
    };
    const { module, store } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();
    await settle();

    // Nobody was signed in, so nothing ended — the surface must not say one did.
    expect(store.getAccessTokenStore().state()).toEqual({ token: null, ended: null });
  });
});

// 2026-08-28-0f46: the reply route dispatches on the request type the answer
// carries. A silent answer belongs to the frame that asked for it: the frame
// notifies the window above and holds nothing of its own, and — critically — it
// does not become this document's returnTo.
describe("a silent return", () => {
  it("SilentReturn_DispatchesOnTheRequestTypeTheAnswerCarries", async () => {
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");
    // signinCallback answers undefined for a silent return: the User belongs to
    // the window that opened the frame, not to the frame.
    const client = { ...fakeClient(), signinCallback: vi.fn(async () => undefined) };
    const { module } = await bootWith(AUTHORITY, client);

    const session = await module.startAuthSession();

    expect(client.signinCallback).toHaveBeenCalledTimes(1);
    expect(session?.isSilentReturn).toBe(true);
    expect(session?.error).toBeNull();
    expect(await module.currentAccessToken()).toBeNull();
  });

  it("SilentReturn_TheFramesAddressBarIsLeftAlone", async () => {
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");
    const client = { ...fakeClient(), signinCallback: vi.fn(async () => undefined) };
    const { module } = await bootWith(AUTHORITY, client);

    await module.startAuthSession();

    // Nobody reads a hidden frame's URL, and the window that does read this
    // answer has already been handed it.
    expect(window.location.search).toBe("?code=the-code&state=the-state");
  });

  it("RedirectReturn_IsStillThisTabsOwnSignIn", async () => {
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");
    const client = fakeClient();
    const { module } = await bootWith(AUTHORITY, client);

    const session = await module.startAuthSession();

    expect(session?.isSilentReturn).toBe(false);
    expect(session?.returnTo).toBe("/jobs");
    expect(await module.currentAccessToken()).toBe("at-1");
  });
});
