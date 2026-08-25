import { describe, it, expect } from "vitest";
import { InMemoryWebStorage } from "oidc-client-ts";
import { authoritySettings, createAuthorityClient } from "../createAuthorityClient";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import type { RuntimeAuthSettings } from "@/lib/runtimeSettings/runtimeSettings";

// 2026-08-25-2de1: an installation that has not configured a directory must be
// unable to attempt a sign-in at all — no client, therefore no redirect. These
// cases also pin the settings the client is built with, because "in memory only"
// is a configuration choice and a default would quietly undo it.

const ORIGIN = "http://localhost:3000";

function auth(overrides: Partial<RuntimeAuthSettings> = {}): RuntimeAuthSettings {
  return {
    ...DEFAULT_RUNTIME_SETTINGS.auth,
    authority: "https://login.example.com/realms/sample",
    clientId: "dashboard",
    ...overrides,
  };
}

describe("createAuthorityClient", () => {
  it("SignIn_NoAuthorityConfigured_IsNeverAttempted", () => {
    expect(createAuthorityClient(DEFAULT_RUNTIME_SETTINGS.auth, ORIGIN)).toBeNull();
  });

  it("SignIn_AuthorityIsBlank_IsNeverAttempted", () => {
    expect(createAuthorityClient(auth({ authority: "   " }), ORIGIN)).toBeNull();
  });

  it("SignIn_NoClientIdentifier_IsNeverAttempted", () => {
    // A public client with no identifier has nothing to identify itself as.
    expect(createAuthorityClient(auth({ clientId: "" }), ORIGIN)).toBeNull();
  });

  it("SignIn_AuthorityConfigured_ThereIsAClient", () => {
    expect(createAuthorityClient(auth(), ORIGIN)).not.toBeNull();
  });
});

describe("authoritySettings", () => {
  it("Settings_AnyAuthority_UseTheAuthorizationCodeFlow", () => {
    expect(authoritySettings(auth(), ORIGIN).response_type).toBe("code");
  });

  it("Settings_AnyAuthority_KeepTheTokenOutOfWebStorage", () => {
    const store = authoritySettings(auth(), ORIGIN).userStore as unknown as {
      _store: unknown;
    };

    expect(store._store).toBeInstanceOf(InMemoryWebStorage);
  });

  it("Settings_AnyAuthority_RenewalIsTheStoresJobNotTheLibrarys", () => {
    // A library renewal that fails leaves the refused token in place.
    expect(authoritySettings(auth(), ORIGIN).automaticSilentRenew).toBe(false);
  });

  it("Settings_RedirectPath_ComposesAgainstThisOrigin", () => {
    expect(authoritySettings(auth(), ORIGIN).redirect_uri).toBe(`${ORIGIN}/signin-callback`);
  });

  it("Settings_NoScopesConfigured_AskForTheOidcFloor", () => {
    expect(authoritySettings(auth({ scopes: "" }), ORIGIN).scope).toBe("openid");
  });

  it("Settings_ScopesConfigured_AreSentAsWritten", () => {
    expect(authoritySettings(auth({ scopes: "openid api" }), ORIGIN).scope).toBe("openid api");
  });

  it("Settings_NoAudienceConfigured_NoneIsSent", () => {
    expect(authoritySettings(auth(), ORIGIN).extraQueryParams).toBeUndefined();
  });

  it("Settings_AudienceConfigured_TravelsWithTheRequest", () => {
    const settings = authoritySettings(auth({ audience: "agentsmith-api" }), ORIGIN);

    expect(settings.extraQueryParams).toEqual({ audience: "agentsmith-api" });
  });
});
