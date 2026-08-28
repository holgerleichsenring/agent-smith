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

  /**
   * 2026-08-28-0f46: this document completed a SILENT return, so it is the
   * hidden frame of a tab that is signing in somewhere else. Everything the
   * answer was for has been handed to that tab; this one has nothing left to do
   * and, above all, nowhere to navigate.
   */
  isSilentReturn = false;

  constructor(client: UserManager, store: AccessTokenStore) {
    this.client = client;
    this.store = store;
    store.renewsWith(() => this.renew());
  }

  /** Boot: the session this tab already holds becomes a token at once. Holding
   *  none, a silent attempt runs against the directory's own session and NOTHING
   *  waits for it — the attempt is the invisible sign-in that was promised; the
   *  wait in front of every request was the defect. */
  async restore(): Promise<void> {
    const held = await this.held();
    if (!held) {
      void this.attemptSilently();
      return;
    }
    if (held.expired !== true) {
      this.hold(held);
      return;
    }
    await this.renewExpired(held);
  }

  /** A person asked to sign in, so the redirect is theirs to expect. */
  async signIn(returnTo: string): Promise<void> {
    await this.client.signinRedirect({ state: { returnTo } });
  }

  /** The authority came back, and WHICH door it came back through is written in
   *  the request its answer carries. A silent return belongs to the frame that
   *  asked for it and is answered by notifying the window above; completing it
   *  as a redirect instead spends the single-use code and removes the state key
   *  the waiting window still needs. A refusal empties the store and is recorded
   *  — it must never reject, or it would take the whole boot down with it. */
  async complete(url?: string): Promise<void> {
    try {
      const user = await this.client.signinCallback(url);
      if (!user) {
        this.isSilentReturn = true;
        return;
      }
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

  private async held(): Promise<User | null> {
    try {
      return await this.client.getUser();
    } catch (cause) {
      console.debug("this tab holds no session it can read back", cause);
      return null;
    }
  }

  private async attemptSilently(): Promise<void> {
    try {
      this.hold(await this.client.signinSilent());
    } catch (cause) {
      console.debug("no directory session to restore — the dashboard stays signed out", cause);
    }
  }

  // An expired token is refused by every call it is sent on, so it is never
  // served. With a refresh token the renewal is one direct token request and
  // answers in a round trip; without one it would be the hidden frame, which is
  // the ten seconds this phase exists to stop every load paying.
  private async renewExpired(held: User): Promise<void> {
    if (!held.refresh_token) return this.discard();
    try {
      this.hold(await this.client.signinSilent());
    } catch (cause) {
      console.debug("the held session is past its expiry and would not renew", cause);
      await this.discard();
    }
  }

  private async discard(): Promise<void> {
    this.store.end("expired");
    await this.client.removeUser();
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
