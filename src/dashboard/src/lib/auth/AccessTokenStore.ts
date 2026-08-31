// 2026-08-25-2de1: the token has one owner, and it is not React. apiFetch and
// JobsHubClient are plain modules that cannot call a hook, so the store is a
// module-level singleton both of them read and the React side reads the same
// one — shaped like loadRuntimeSettings, for the same reason.
//
// 2026-08-28-0f46: the access token is still held in a closure here and still
// dies with the tab. What no longer holds is the claim this header used to make
// about the system around it: the SESSION it is minted from is now kept in
// session storage (createAuthorityClient) so that a reload finds it, and the
// larger credential in that session is the refresh token, not this one. The
// exposure that buys is written down in the phase, and what retires it —
// custody moved to the server, behind a cookie the page cannot read — is a
// phase of its own rather than a promise made in a comment.

/** An access token and the moment the authority stops accepting it. */
export interface AccessTokenGrant {
  accessToken: string;
  /** Epoch milliseconds. Absent when the authority named no expiry. */
  expiresAt?: number;
}

/**
 * Why a session that HAD a token no longer has one. Null is not one of these:
 * it is the state of a tab that never signed in, and a surface that cannot tell
 * the two apart renders the same bare button for both — which is what sent an
 * operator through the configuration looking for a fault in their browser.
 */
export type SessionEndReason = "expired" | "renewal-refused";

/** What the store announces: the token to send, and why there is none. */
export interface AccessTokenState {
  token: string | null;
  ended: SessionEndReason | null;
}

/** Asks the authority for a fresh grant. Null means it would not give one. */
export type AccessTokenRenewal = () => Promise<AccessTokenGrant | null>;

// Renewal starts this far ahead of expiry. A tab open longer than a token lives
// has to keep working, and a renewal that begins after the first refusal has
// already cost the operator a failed call they did not cause.
const RENEW_AHEAD_MS = 60_000;

export type AccessTokenListener = (state: AccessTokenState) => void;

export class AccessTokenStore {
  private token: string | null = null;
  private ended: SessionEndReason | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private renewal: AccessTokenRenewal | null = null;
  private readonly listeners = new Set<AccessTokenListener>();

  /** The token to send, or null when nobody is signed in. */
  read(): string | null {
    return this.token;
  }

  /** The token and the reason there is none, as one answer. */
  state(): AccessTokenState {
    return { token: this.token, ended: this.ended };
  }

  /** Names who to ask when the held token is about to expire. */
  renewsWith(renewal: AccessTokenRenewal): void {
    this.renewal = renewal;
  }

  hold(grant: AccessTokenGrant): void {
    this.token = grant.accessToken;
    this.ended = null;
    this.schedule(grant.expiresAt);
    this.announce();
  }

  /** The session ends with nothing to say — a sign-out, or one that never was. */
  clear(): void {
    this.forget(null);
  }

  // A refused token and no token fail every call the same way, and only one of
  // the two reads as a server fault. The store empties rather than serve one —
  // and says which of the two emptied it, because the person looking at the
  // surface cannot tell from a button that reads the same either way.
  /** The session ends for a reason the surface has to be able to name. */
  end(reason: SessionEndReason): void {
    this.forget(reason);
  }

  subscribe(listener: AccessTokenListener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private forget(reason: SessionEndReason | null): void {
    this.token = null;
    this.ended = reason;
    this.unschedule();
    this.announce();
  }

  private schedule(expiresAt?: number): void {
    this.unschedule();
    if (expiresAt === undefined) return;
    const delay = Math.max(0, expiresAt - RENEW_AHEAD_MS - Date.now());
    this.timer = setTimeout(() => void this.renew(), delay);
  }

  private unschedule(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
  }

  private async renew(): Promise<void> {
    try {
      const grant = await this.renewal?.();
      if (grant) {
        this.hold(grant);
        return;
      }
      console.warn("the authority would not renew the access token — the dashboard is signed out");
    } catch (cause) {
      console.warn("the access token could not be renewed — the dashboard is signed out", cause);
    }
    this.end("renewal-refused");
  }

  private announce(): void {
    const state = this.state();
    for (const listener of this.listeners) listener(state);
  }
}

let singleton: AccessTokenStore | null = null;

/** The one store this tab has. */
export function getAccessTokenStore(): AccessTokenStore {
  singleton ??= new AccessTokenStore();
  return singleton;
}

/** The accessor a plain module reaches for. Null means no token is held. */
export function getAccessToken(): string | null {
  return getAccessTokenStore().read();
}

/** Test-only: reset the module-level singleton between tests. */
export function __resetAccessTokenStoreForTests(): void {
  singleton = null;
}
