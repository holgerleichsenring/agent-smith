import {
  InMemoryWebStorage,
  UserManager,
  WebStorageStateStore,
  type UserManagerSettings,
} from "oidc-client-ts";
import type { RuntimeAuthSettings } from "@/lib/runtimeSettings/runtimeSettings";

// 2026-08-25-2de1: authorization code with PKCE against whatever authority the
// settings document names, discovered rather than described. p0503b validates
// ONE OIDC issuer and the server's own configuration names a Keycloak realm as a
// first-class case, so a client written for a single directory would make the
// dashboard work against a subset of the authorities the server already accepts —
// and the mismatch would surface as an issuer that simply does not work.
//
// A public client, no secret: the settings document is served to any browser that
// asks, and it is the only place these values can come from.

// The floor an OIDC request cannot go below — an authority handed an empty scope
// is being asked for nothing at all.
const MINIMUM_SCOPE = "openid";

/**
 * The sign-in client for this installation, or null when no authority is
 * configured — the state in which no redirect is possible and every call goes
 * out exactly as it does today. This mirrors the server, which registers no
 * validation without an authority either.
 */
export function createAuthorityClient(
  auth: RuntimeAuthSettings,
  origin: string,
): UserManager | null {
  if (!auth.authority.trim() || !auth.clientId.trim()) return null;
  return new UserManager(authoritySettings(auth, origin));
}

/** What the client is configured with, separately so a test can drive the real
 *  library against a stubbed authority rather than a stand-in for the library. */
export function authoritySettings(auth: RuntimeAuthSettings, origin: string): UserManagerSettings {
  return {
    authority: auth.authority.trim(),
    client_id: auth.clientId.trim(),
    redirect_uri: new URL(auth.redirectPath, origin).toString(),
    post_logout_redirect_uri: new URL("/", origin).toString(),
    response_type: "code",
    scope: auth.scopes.trim() || MINIMUM_SCOPE,
    // The token lives in memory and dies with the tab. The library's own default
    // here is session storage, which anything achieving script execution in the
    // page can read.
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    // The PKCE verifier and the anti-forgery state are the one thing memory cannot
    // hold: they have to survive the navigation to the authority and back. Neither
    // is a credential and both are worthless without the authorization code they
    // were minted for. Session storage drops them with the tab; the library's own
    // default here is local storage, which outlives it.
    stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
    // AccessTokenStore schedules renewal itself, because a library renewal that
    // fails leaves the refused token in place — and a token the server refuses
    // fails every call in a way that reads as a server fault.
    automaticSilentRenew: false,
    monitorSession: false,
    // Authorities that partition tokens by audience read this; the rest ignore
    // an unknown parameter, which is why it is only sent when one is configured.
    ...(auth.audience.trim() ? { extraQueryParams: { audience: auth.audience.trim() } } : {}),
  };
}
