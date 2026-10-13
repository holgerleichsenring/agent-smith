// 2026-10-13-9a59p: one pending refetch for every nudge that asks for one. A nudge names the
// window it may wait; the refetch fires at the EARLIEST due time any pending nudge asked for, so
// a burst inside one window costs one fetch and a 2 s nudge is never held back by a 15 s one.
// A throttle, not a trailing debounce: a steady stream still refreshes once per window.

export class CoalescedRefetch {
  private timer: ReturnType<typeof setTimeout> | null = null;
  private dueAt = 0;

  constructor(private readonly fire: () => void) {}

  within(windowMs: number): void {
    const due = Date.now() + windowMs;
    if (this.timer !== null && this.dueAt <= due) return;
    this.cancel();
    this.dueAt = due;
    this.timer = setTimeout(() => {
      this.timer = null;
      this.fire();
    }, windowMs);
  }

  cancel(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
  }
}
