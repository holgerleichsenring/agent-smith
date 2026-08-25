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

/** Only the four members AuthSession reaches for. */
function fakeClient() {
  return {
    signinSilent: vi.fn(async () => null),
    signinRedirect: vi.fn(async () => undefined),
    signinRedirectCallback: vi.fn(async () => ({
      access_token: "at-1",
      expires_at: Math.floor(Date.now() / 1000) + 3600,
      state: { returnTo: "/jobs" },
    })),
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
  return { module: await import("../session"), created };
}

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

    expect(client.signinRedirectCallback).toHaveBeenCalledTimes(1);
    expect(client.signinSilent).not.toHaveBeenCalled();
    expect(await module.currentAccessToken()).toBe("at-1");
    expect(session?.returnTo).toBe("/jobs");
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
