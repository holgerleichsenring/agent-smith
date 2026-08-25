// 2026-08-25-2de1: the token has one owner, and it is not React. apiFetch and
// JobsHubClient are plain modules that cannot call a hook, so the store is a
// module-level singleton both of them read and the React side reads the same
// one — shaped like loadRuntimeSettings, for the same reason.
//
// NOTHING HERE TOUCHES WEB STORAGE. This value is equivalent to run control, the
// config store and every secret the config store guards, and anything achieving
// script execution in the page can read localStorage and sessionStorage. Held in
// a closure it dies with the tab, and the authority's own session — which is what
// a directory exists to hold — makes the next load a redirect nobody sees.

/** An access token and the moment the authority stops accepting it. */
export interface AccessTokenGrant {
  accessToken: string;
  /** Epoch milliseconds. Absent when the authority named no expiry. */
  expiresAt?: number;
}

/** Asks the authority for a fresh grant. Null means it would not give one. */
export type AccessTokenRenewal = () => Promise<AccessTokenGrant | null>;

// Renewal starts this far ahead of expiry. A tab open longer than a token lives
// has to keep working, and a renewal that begins after the first refusal has
// already cost the operator a failed call they did not cause.
const RENEW_AHEAD_MS = 60_000;

export type AccessTokenListener = (token: string | null) => void;

export class AccessTokenStore {
  private token: string | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private renewal: AccessTokenRenewal | null = null;
  private readonly listeners = new Set<AccessTokenListener>();

  /** The token to send, or null when nobody is signed in. */
  read(): string | null {
    return this.token;
  }

  /** Names who to ask when the held token is about to expire. */
  renewsWith(renewal: AccessTokenRenewal): void {
    this.renewal = renewal;
  }

  hold(grant: AccessTokenGrant): void {
    this.token = grant.accessToken;
    this.schedule(grant.expiresAt);
    this.announce();
  }

  // A refused token and no token fail every call the same way, and only one of
  // the two reads as a server fault. The store empties rather than serve one.
  clear(): void {
    this.token = null;
    this.unschedule();
    this.announce();
  }

  subscribe(listener: AccessTokenListener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
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
    this.clear();
  }

  private announce(): void {
    for (const listener of this.listeners) listener(this.token);
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
