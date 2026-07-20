import { describe, it, expect } from "vitest";
import { ScopeBuffer } from "../scopeBuffer";

const flush = (): Promise<void> => new Promise((r) => setTimeout(r, 0));

// An opener that replays a fixed window every time it (re)starts — mirrors the
// hub's XRANGE replay on SubscribeSystem.
function replaying(window: string[]) {
  return (push: (e: string) => void) => {
    window.forEach(push);
    return Promise.resolve(async () => {});
  };
}

describe("ScopeBuffer", () => {
  it("Keyed_ReAcquireReplay_DoesNotDuplicate", async () => {
    const buf = new ScopeBuffer<string>(500, replaying(["a", "b", "c"]), (e) => e);

    const release = buf.acquire();
    await flush();
    expect(buf.getSnapshot()).toEqual(["a", "b", "c"]);

    release(); // backlog kept (p0218)
    await flush();
    buf.acquire(); // re-acquire → opener replays a,b,c again
    await flush();

    expect(buf.getSnapshot()).toEqual(["a", "b", "c"]); // not 6
  });

  it("Unkeyed_ReAcquireReplay_AppendsAgain", async () => {
    // The per-run scope is unkeyed: identical events are legitimate, so a
    // re-replay does append. Documents why keyOf is opt-in.
    const buf = new ScopeBuffer<string>(500, replaying(["a", "b"]));

    const release = buf.acquire();
    await flush();
    release();
    await flush();
    buf.acquire();
    await flush();

    expect(buf.getSnapshot()).toEqual(["a", "b", "a", "b"]);
  });

  it("PushMany_SeedsBatch_InOneNotification", async () => {
    let notifications = 0;
    const buf = new ScopeBuffer<string>(
      500,
      (_push, pushMany) => {
        pushMany(["a", "b", "c"]);
        return Promise.resolve(async () => {});
      },
      (e) => e,
    );
    buf.subscribeChange(() => notifications++);

    buf.acquire();
    await flush();

    expect(buf.getSnapshot()).toEqual(["a", "b", "c"]);
    expect(notifications).toBe(1); // one render for the whole batch, not three
  });

  it("PushMany_DedupsAgainstExistingBacklog", async () => {
    // A re-seeded backlog batch (reconnect/remount) must not duplicate.
    const buf = new ScopeBuffer<string>(
      500,
      (_push, pushMany) => {
        pushMany(["a", "b"]);
        return Promise.resolve(async () => {});
      },
      (e) => e,
    );

    const release = buf.acquire();
    await flush();
    release();
    await flush();
    buf.acquire();
    await flush();

    expect(buf.getSnapshot()).toEqual(["a", "b"]);
  });

  it("Push_Burst_CoalescesToOneNotification", async () => {
    // p0355: a burst of individual pushes must cost ONE listener notification
    // (one render), not one per event — the backlog itself still merges
    // synchronously, so getSnapshot sees everything.
    let notifications = 0;
    const buf = new ScopeBuffer<string>(500, (push) => {
      push("a");
      push("b");
      push("c");
      return Promise.resolve(async () => {});
    });
    buf.subscribeChange(() => notifications++);

    buf.acquire();
    await flush();

    expect(buf.getSnapshot()).toEqual(["a", "b", "c"]);
    expect(notifications).toBe(1);
  });

  it("Keyed_EvictedKeyPastCap_NotFalselyDeduped", async () => {
    // cap=2: pushing past the cap must forget the evicted key so the same value
    // can legitimately re-enter later.
    const buf = new ScopeBuffer<string>(2, (push) => {
      push("a");
      push("b");
      push("c"); // evicts "a"
      push("a"); // genuine new arrival — must not be dropped
      return Promise.resolve(async () => {});
    }, (e) => e);

    buf.acquire();
    await flush();

    expect(buf.getSnapshot()).toEqual(["c", "a"]);
  });
});
