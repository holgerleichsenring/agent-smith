import { loadRuntimeSettings } from "@/lib/runtimeSettings/runtimeSettings";
import { AuthSession } from "./AuthSession";
import { getAccessToken, getAccessTokenStore } from "./AccessTokenStore";
import { createAuthorityClient } from "./createAuthorityClient";

// 2026-08-25-2de1: one loop per tab, resolved once. Two would give the tab two
// tokens and a window in which the REST calls and the hub disagree about who is
// signed in. A module-level promise for the same reason loadRuntimeSettings is
// one: apiFetch and JobsHubClient are not components and cannot call a hook.
//
// Every outgoing call AWAITS this promise. Firing the calls first and signing in
// alongside them would make the first render of every page load a burst of
// requests carrying no token — which, the moment an installation enforces, is a
// burst of 401s nobody caused.

let boot: Promise<AuthSession | null> | null = null;

/** The sign-in loop for this tab, or null when no authority is configured. */
export function startAuthSession(): Promise<AuthSession | null> {
  boot ??= begin();
  return boot;
}

async function begin(): Promise<AuthSession | null> {
  // None of this has meaning during a prerender: the redirect URI is relative to
  // the browser's origin, and there is no browser.
  if (typeof window === "undefined") return null;
  const { auth } = await loadRuntimeSettings();
  const client = createAuthorityClient(auth, window.location.origin);
  if (!client) return null;
  const session = new AuthSession(client, getAccessTokenStore());
  // On the callback route the code exchange IS this load's sign-in. A silent
  // attempt beside it would be a second one against the same directory session.
  if (isCallback(auth.redirectPath)) {
    await session.complete();
    forgetAuthorityAnswer();
  } else {
    await session.restore();
  }
  return session;
}

// An authorization code is single-use, and after the exchange it is spent. Left
// in the address bar it survives into history and into whatever the person
// copies out of it, and the next reload of that URL exchanges it a second time —
// which the authority refuses, emptying a store that was correctly filled a
// moment ago. Only the authority's own parameters go; a route's own query is
// none of this function's business.
const AUTHORITY_ANSWER = ["code", "state", "session_state", "error", "error_description", "iss"];

function forgetAuthorityAnswer(): void {
  const url = new URL(window.location.href);
  if (!AUTHORITY_ANSWER.some((key) => url.searchParams.has(key))) return;
  for (const key of AUTHORITY_ANSWER) url.searchParams.delete(key);
  window.history.replaceState(window.history.state, "", `${url.pathname}${url.search}${url.hash}`);
}

// The path alone does not settle it. An authority whose reply URL is registered
// WITHOUT a path returns to "/", and a dashboard that read every visit to its own
// home page as a callback would run the code exchange against a URL carrying no
// code — which fails, records a refusal and empties the store, so the silent
// restore that should have happened there never runs and the tab is permanently
// signed out. The authority's own answer is what marks the arrival: an
// authorization code, or the error it sends instead.
function isCallback(redirectPath: string): boolean {
  if (window.location.pathname !== new URL(redirectPath, window.location.origin).pathname) {
    return false;
  }
  const answer = new URLSearchParams(window.location.search);
  return answer.has("code") || answer.has("error");
}

/** The token an outgoing call carries, once the loop has settled. */
export async function currentAccessToken(): Promise<string | null> {
  await startAuthSession();
  return getAccessToken();
}

/** A person asked to sign in. No authority configured is never attempted. */
export async function signIn(returnTo?: string): Promise<void> {
  const session = await startAuthSession();
  await session?.signIn(returnTo ?? here());
}

/** Ends the local session, and the authority's where it publishes one. */
export async function signOut(): Promise<void> {
  const session = await startAuthSession();
  await session?.signOut();
}

function here(): string {
  return `${window.location.pathname}${window.location.search}`;
}

/** Test-only: reset the module-level boot between tests. */
export function __resetAuthSessionForTests(): void {
  boot = null;
}
