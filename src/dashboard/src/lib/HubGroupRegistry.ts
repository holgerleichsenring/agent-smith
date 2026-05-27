// p0169f: ref-counts active group subscriptions so a single Hub group is
// only subscribed once per browser tab even when multiple React components
// ask for the same data. On first incRef the caller runs the actual
// hub-method invocation; on last decRef the caller runs the
// inverse-invocation. Pure counter, no IO.

export class HubGroupRegistry {
  private readonly counts = new Map<string, number>();

  /**
   * @returns true when this is the FIRST subscriber for the key
   *   (caller should issue the actual hub.invoke). false otherwise
   *   (an existing subscription is reused).
   */
  incRef(key: string): boolean {
    const before = this.counts.get(key) ?? 0;
    this.counts.set(key, before + 1);
    return before === 0;
  }

  /**
   * @returns true when this drops to zero subscribers (caller should issue
   *   the inverse hub.invoke). false otherwise.
   */
  decRef(key: string): boolean {
    const before = this.counts.get(key) ?? 0;
    if (before <= 0) return false;
    const after = before - 1;
    if (after === 0) {
      this.counts.delete(key);
      return true;
    }
    this.counts.set(key, after);
    return false;
  }

  count(key: string): number {
    return this.counts.get(key) ?? 0;
  }

  reset(): void {
    this.counts.clear();
  }
}
