import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { AccessTokenStore, getAccessToken, getAccessTokenStore } from "../AccessTokenStore";

// 2026-08-25-2de1: a tab open longer than a token lives has to keep working, and
// a store that keeps serving a token the server refuses turns every call into
// what reads like a server fault. These cases pin both halves.

const MINUTE = 60_000;

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(console, "warn").mockImplementation(() => {});
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("AccessTokenStore", () => {
  it("Store_NothingHeld_ReadsAsNoToken", () => {
    expect(new AccessTokenStore().read()).toBeNull();
  });

  it("Store_TokenHeld_ReadsItBack", () => {
    const store = new AccessTokenStore();

    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });

    expect(store.read()).toBe("t-1");
  });

  it("Store_TokenNearExpiry_RenewsBeforeItExpires", async () => {
    const store = new AccessTokenStore();
    const renewal = vi.fn(async () => ({
      accessToken: "t-2",
      expiresAt: Date.now() + 10 * MINUTE,
    }));
    store.renewsWith(renewal);

    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });
    // Nine minutes in: still a minute of life left, and already renewed.
    await vi.advanceTimersByTimeAsync(9 * MINUTE);

    expect(renewal).toHaveBeenCalledTimes(1);
    expect(store.read()).toBe("t-2");
  });

  it("Store_RenewalFails_TheStoreEmptiesRatherThanServingADeadToken", async () => {
    const store = new AccessTokenStore();
    store.renewsWith(() => Promise.reject(new Error("the authority refused")));

    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });
    await vi.advanceTimersByTimeAsync(9 * MINUTE);

    expect(store.read()).toBeNull();
  });

  it("Store_RenewalDeclines_TheStoreEmpties", async () => {
    const store = new AccessTokenStore();
    store.renewsWith(async () => null);

    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });
    await vi.advanceTimersByTimeAsync(9 * MINUTE);

    expect(store.read()).toBeNull();
  });

  it("Store_NoRenewalRegistered_TheStoreEmptiesAtTheRenewalPoint", async () => {
    const store = new AccessTokenStore();

    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });
    await vi.advanceTimersByTimeAsync(9 * MINUTE);

    expect(store.read()).toBeNull();
  });

  it("Store_AuthorityNamedNoExpiry_NothingIsScheduled", async () => {
    const store = new AccessTokenStore();
    const renewal = vi.fn(async () => null);
    store.renewsWith(renewal);

    store.hold({ accessToken: "t-1" });
    await vi.advanceTimersByTimeAsync(24 * 60 * MINUTE);

    expect(renewal).not.toHaveBeenCalled();
    expect(store.read()).toBe("t-1");
  });

  it("Store_Cleared_TheScheduledRenewalIsDroppedToo", async () => {
    const store = new AccessTokenStore();
    const renewal = vi.fn(async () => null);
    store.renewsWith(renewal);
    store.hold({ accessToken: "t-1", expiresAt: Date.now() + 10 * MINUTE });

    store.clear();
    await vi.advanceTimersByTimeAsync(9 * MINUTE);

    expect(renewal).not.toHaveBeenCalled();
  });

  it("Store_Subscribed_EveryChangeReachesTheListenerUntilItUnsubscribes", () => {
    const store = new AccessTokenStore();
    const seen: Array<string | null> = [];
    const stop = store.subscribe((token) => seen.push(token));

    store.hold({ accessToken: "t-1" });
    store.clear();
    stop();
    store.hold({ accessToken: "t-2" });

    expect(seen).toEqual(["t-1", null]);
  });
});

describe("getAccessTokenStore", () => {
  it("Store_ReadTwice_IsTheSameStore", () => {
    // apiFetch and JobsHubClient must not be able to disagree about the token.
    expect(getAccessTokenStore()).toBe(getAccessTokenStore());
  });

  it("AccessToken_HeldBySingleton_IsWhatThePlainAccessorReturns", () => {
    getAccessTokenStore().hold({ accessToken: "t-9" });

    expect(getAccessToken()).toBe("t-9");

    getAccessTokenStore().clear();
  });
});
