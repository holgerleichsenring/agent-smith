import type { TicketDefaults } from "@/types/tickets";
import { CoalescedRefetch } from "./CoalescedRefetch";
import { failure } from "./readFailure";
import type { Read } from "./TicketPageReads";

// 2026-10-13-8762q: the ticket page's defaults read, composed by TicketPageReads with its own 2 s window, so a
// default taken in the live attempt or a verdict given here re-reads the defaults and nothing else.

export class TicketDefaultsRead {
  private readonly tick = new CoalescedRefetch(() => void this.read());
  private disposed = false;
  private generation = 0;
  private abort = new AbortController();

  constructor(
    private readonly load: (signal: AbortSignal) => Promise<TicketDefaults>,
    private readonly held: () => Read<TicketDefaults>,
    private readonly landed: (read: Read<TicketDefaults>) => void,
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
