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
  await (isCallback(auth.redirectPath) ? session.complete() : session.restore());
  return session;
}

function isCallback(redirectPath: string): boolean {
  return window.location.pathname === new URL(redirectPath, window.location.origin).pathname;
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
