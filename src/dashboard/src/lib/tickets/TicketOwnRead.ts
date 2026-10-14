import { CoalescedRefetch } from "./CoalescedRefetch";
import { failure } from "./readFailure";
import type { Read } from "./TicketPageReads";

// A ticket-level read on its own 2 s window, composed by TicketPageReads: the review rounds (2026-10-13-8762l),
// the defaults (2026-10-13-8762q) and the ticket text each re-read on their own nudges and never the
// detail, the series or the conversation.

export class TicketOwnRead<T> {
  private readonly tick = new CoalescedRefetch(() => void this.read());
  private disposed = false;
  private generation = 0;
  private abort = new AbortController();

  constructor(
    private readonly load: (signal: AbortSignal) => Promise<T>,
    private readonly held: () => Read<T>,
    private readonly landed: (read: Read<T>) => void,
  ) {}

  /** Restartable, like the page's reads. */
  start(): void {
    this.disposed = false;
    this.abort = new AbortController();
    void this.read();
  }

  dispose(): void {
    this.disposed = true;
    this.generation += 1;
    this.tick.cancel();
    this.abort.abort();
  }

  /** 2026-10-14-dbcen: one re-read now, past any pending window — after this page's own act. */
  now(): void {
    this.tick.cancel();
    void this.read();
  }

  /** One re-read within the window, however many nudges ask for it. */
  within(windowMs: number): void {
    this.tick.within(windowMs);
  }

  private async read(): Promise<void> {
    const generation = this.generation;
    try {
      const data = await this.load(this.abort.signal);
      if (!this.disposed && generation === this.generation) this.landed({ data, failed: false, refusal: null, loading: false });
    } catch (thrown) {
      if (!this.disposed && generation === this.generation) this.landed(failure(thrown, this.held()));
    }
  }
}
