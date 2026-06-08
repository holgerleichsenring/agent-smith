// p0218: one scope's persistent capped backlog plus a ref-counted live
// subscription. The backlog lives here — above any component — so navigating
// between views does NOT wipe it (the old useSystemEvents reset setEvents([])
// on every mount). The subscription opens on the first consumer and closes on
// the last, with the same cancelled-before-resolve guard the hooks used.

// `push` appends one live event; `pushMany` seeds a backfill batch in a single
// notification (one render) instead of one-per-event. Most openers use only push.
type Opener<T> = (
  push: (event: T) => void,
  pushMany: (events: T[]) => void,
) => Promise<() => Promise<void>>;

export class ScopeBuffer<T> {
  private backlog: T[] = [];
  private readonly changeListeners = new Set<() => void>();
  private refs = 0;
  private cancel: (() => Promise<void>) | null = null;
  private cancelled = false;
  // Keys of the events currently in the backlog — only tracked when keyOf is
  // set. Lets push drop an event we already hold instead of appending it again.
  private readonly seen = new Set<string>();

  constructor(
    private readonly cap: number,
    private readonly open: Opener<T>,
    // Optional identity for an event. When set, a re-emitted event with a key
    // already in the backlog is dropped. The opener replays the FULL retained
    // window every time it (re)starts (the hub's XRANGE replay on Subscribe*),
    // and the backlog is deliberately kept across release→acquire (p0218); so
    // without this guard a reconnect / StrictMode remount would append the whole
    // window a second time and the view would "run through everything again".
    // Left unset for the per-run scope, where identical events are legitimate.
    private readonly keyOf?: (event: T) => string,
  ) {}

  // Stable identities (class-field arrows) so useSyncExternalStore does not
  // resubscribe every render.
  getSnapshot = (): T[] => this.backlog;

  subscribeChange = (listener: () => void): (() => void) => {
    this.changeListeners.add(listener);
    return () => this.changeListeners.delete(listener);
  };

  /** Open the live subscription on 0→1; returns a release for the caller. */
  acquire(): () => void {
    if (++this.refs === 1) this.start();
    return () => this.release();
  }

  private release(): void {
    if (this.refs > 0 && --this.refs === 0) this.stop();
  }

  private start(): void {
    this.cancelled = false;
    this.open((event) => this.push(event), (events) => this.pushMany(events))
      .then((cancel) => {
        if (this.cancelled) cancel();
        else this.cancel = cancel;
      })
      .catch(() => {
        /* hub transitioning; backlog is kept, a later acquire retries */
      });
  }

  private stop(): void {
    this.cancelled = true;
    this.cancel?.();
    this.cancel = null;
  }

  private push(event: T): void {
    this.pushMany([event]);
  }

  // Append a batch in one shot: dedups (when keyed), trims to cap, and notifies
  // listeners ONCE so a backfill seeds in a single render rather than stepping
  // through one event at a time.
  private pushMany(events: T[]): void {
    const fresh: T[] = [];
    for (const event of events) {
      if (this.keyOf) {
        const key = this.keyOf(event);
        if (this.seen.has(key)) continue; // replayed duplicate — already held
        this.seen.add(key);
      }
      fresh.push(event);
    }
    if (fresh.length === 0) return;
    let next = [...this.backlog, ...fresh];
    if (next.length > this.cap) {
      const overflow = next.length - this.cap;
      if (this.keyOf) {
        for (let i = 0; i < overflow; i++) this.seen.delete(this.keyOf(next[i]));
      }
      next = next.slice(overflow);
    }
    this.backlog = next;
    for (const listener of this.changeListeners) listener();
  }
}
