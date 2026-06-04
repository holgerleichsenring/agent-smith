// p0218: one scope's persistent capped backlog plus a ref-counted live
// subscription. The backlog lives here — above any component — so navigating
// between views does NOT wipe it (the old useSystemEvents reset setEvents([])
// on every mount). The subscription opens on the first consumer and closes on
// the last, with the same cancelled-before-resolve guard the hooks used.

type Opener<T> = (push: (event: T) => void) => Promise<() => Promise<void>>;

export class ScopeBuffer<T> {
  private backlog: T[] = [];
  private readonly changeListeners = new Set<() => void>();
  private refs = 0;
  private cancel: (() => Promise<void>) | null = null;
  private cancelled = false;

  constructor(
    private readonly cap: number,
    private readonly open: Opener<T>,
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
    this.open((event) => this.push(event))
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
    const next = [...this.backlog, event];
    this.backlog = next.length > this.cap ? next.slice(next.length - this.cap) : next;
    for (const listener of this.changeListeners) listener();
  }
}
