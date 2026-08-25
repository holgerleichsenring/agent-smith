import type { User, UserManager } from "oidc-client-ts";
import type { AccessTokenGrant, AccessTokenStore } from "./AccessTokenStore";

// 2026-08-25-2de1: the sign-in loop for one tab.
//
// Boot attempts a SILENT sign-in and nothing else. An automatic interactive
// redirect on boot is a redirect loop waiting to happen, and it would take over
// a dashboard whose server enforces nothing yet. Silent succeeds against a live
// directory session and is invisible; when it fails the person stays signed out
// and every surface keeps working. The visible sign-in is someone pressing
// something, which is signIn().

/** Where a completed sign-in returns to when it carried no route of its own. */
const HOME = "/";

export class AuthSession {
  private readonly client: UserManager;
  private readonly store: AccessTokenStore;

  /** The route this tab was on when the redirect took it away. */
  returnTo = HOME;

  /** What the authority refused with, for whichever surface reports it. */
  error: string | null = null;

  constructor(client: UserManager, store: AccessTokenStore) {
    this.client = client;
    this.store = store;
    store.renewsWith(() => this.renew());
  }

  /** Boot: an existing directory session becomes a token, invisibly. Failing
   *  means signed out, which is a state rather than a fault. */
  async restore(): Promise<void> {
    try {
      this.hold(await this.client.signinSilent());
    } catch (cause) {
      console.debug("no directory session to restore — the dashboard stays signed out", cause);
    }
  }

  /** A person asked to sign in, so the redirect is theirs to expect. */
  async signIn(returnTo: string): Promise<void> {
    await this.client.signinRedirect({ state: { returnTo } });
  }

  /** The authority came back. A refusal empties the store and is recorded —
   *  it must never reject, or it would take the whole boot down with it. */
  async complete(url?: string): Promise<void> {
    try {
      const user = await this.client.signinRedirectCallback(url);
      this.returnTo = routeOf(user) ?? HOME;
      this.hold(user);
    } catch (cause) {
      this.error = cause instanceof Error ? cause.message : String(cause);
      this.store.clear();
      console.warn("the authority did not complete the sign-in", cause);
    }
  }

  /** Dropping only the in-memory token leaves the authority's session standing,
   *  so the next silent renewal signs the same person straight back in. */
  async signOut(): Promise<void> {
    this.store.clear();
    await this.client.removeUser();
    if (await this.publishesEndSession()) await this.client.signoutRedirect();
  }

  private async renew(): Promise<AccessTokenGrant | null> {
    return grantOf(await this.client.signinSilent());
  }

  private hold(user: User | null): void {
    const grant = grantOf(user);
    if (grant) this.store.hold(grant);
    else this.store.clear();
  }

  // An authority that publishes no end-session endpoint is not an error; the
  // local session still ends, and asking it to end its own is simply not offered.
  private async publishesEndSession(): Promise<boolean> {
    try {
      return Boolean(await this.client.metadataService.getEndSessionEndpoint());
    } catch (cause) {
      console.debug("the authority's metadata offers no end-session endpoint", cause);
      return false;
    }
  }
}

function grantOf(user: User | null): AccessTokenGrant | null {
  if (!user?.access_token) return null;
  // expires_at is epoch seconds on the wire; the store schedules in milliseconds.
  return {
    accessToken: user.access_token,
    expiresAt: user.expires_at === undefined ? undefined : user.expires_at * 1000,
  };
}

function routeOf(user: User): string | null {
  const state = user.state as { returnTo?: unknown } | null | undefined;
  return typeof state?.returnTo === "string" ? state.returnTo : null;
}
