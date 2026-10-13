import { ApiResponseError, refusalIn, type ApiRefusal } from "@/lib/apiResponse";
import { isRunLive } from "@/lib/runLiveness";
import {
  fetchRunPasses,
  fetchTicketConversation,
  fetchTicketDetail,
  fetchTicketSeries,
} from "@/lib/ticketsApi";
import type { RunPasses, TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";
import { CoalescedRefetch } from "./CoalescedRefetch";

// 2026-10-13-9a59q: every read of one ticket page, held once for the page's life. Detail, the default
// series and the conversation are ticket-level; series?runId= and passes are per attempt. A terminal
// attempt's per-run reads never change, so they are fetched once; a live attempt's (isRunLive — a
// question park resumes the same run and adds passes) are re-read with the detail. Every read is
// scheduled through its own 2 s window, so a burst of nudges costs one fetch per read.

export const TICKET_READ_MS = 2000;

export interface Read<T> {
  data: T | null;
  failed: boolean;
  refusal: ApiRefusal | null;
  /** True while the first answer is outstanding. */
  loading: boolean;
  /** The server answered 404: no such ticket, or no such attempt of it. */
  notFound?: boolean;
}

export interface TicketPageSnapshot {
  detail: Read<TicketDetail>;
  series: Read<TicketSeries>;
  conversation: Read<TicketConversation>;
  runSeries: Readonly<Record<string, Read<TicketSeries>>>;
  runPasses: Readonly<Record<string, Read<RunPasses>>>;
}

export interface TicketPageApi {
  detail(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketDetail>;
  series(project: string, ticketId: string, runId: string | null, signal?: AbortSignal): Promise<TicketSeries>;
  conversation(project: string, ticketId: string, signal?: AbortSignal): Promise<TicketConversation>;
  passes(runId: string, signal?: AbortSignal): Promise<RunPasses>;
}

/** The reads over HTTP, bound at call time. */
export const TICKET_PAGE_API: TicketPageApi = {
  detail: (project, ticketId, signal) => fetchTicketDetail(project, ticketId, signal),
  series: (project, ticketId, runId, signal) => fetchTicketSeries(project, ticketId, runId, signal),
  conversation: (project, ticketId, signal) => fetchTicketConversation(project, ticketId, signal),
  passes: (runId, signal) => fetchRunPasses(runId, signal),
};

export type PerRunRead = "series" | "passes";

const PENDING: Read<never> = { data: null, failed: false, refusal: null, loading: true };

export class TicketPageReads {
  private snap: TicketPageSnapshot = {
    detail: PENDING, series: PENDING, conversation: PENDING, runSeries: {}, runPasses: {},
  };
  private readonly wanted = new Set<string>();
  private readonly inFlight = new Set<string>();
  private readonly again = new Set<string>();
  private readonly ticketTick = new CoalescedRefetch(() => this.readTicket());
  private readonly conversationTick = new CoalescedRefetch(() => this.readConversation());
  private lastStatuses: string | null = null;
  private disposed = false;
  private generation = 0;
  private abort = new AbortController();

  constructor(
    readonly project: string,
    readonly ticketId: string,
    private readonly onChange: (snapshot: TicketPageSnapshot) => void,
    private readonly api: TicketPageApi = TICKET_PAGE_API,
  ) {}

  get snapshot(): TicketPageSnapshot {
    return this.snap;
  }

  /** Restartable: React's strict mode disposes and starts an effect once more. */
  start(): void {
    this.disposed = false;
    this.abort = new AbortController();
    this.readTicket();
    this.readConversation();
    for (const key of this.wanted) {
      const [kind, runId] = splitKey(key);
      if (!this.perRun(kind)[runId]?.data && !this.inFlight.has(key)) this.readPerRun(kind, runId);
    }
  }

  dispose(): void {
    this.disposed = true;
    this.generation += 1;
    this.inFlight.clear();
    this.again.clear();
    this.ticketTick.cancel();
    this.conversationTick.cancel();
    this.abort.abort();
  }

  /** (a) a TicketActivityChanged naming this ticket: everything, the conversation included. */
  ticketChanged(): void {
    this.ticketTick.within(TICKET_READ_MS);
    this.conversationTick.within(TICKET_READ_MS);
  }

  /** (b) a RunsChanged — counted only for one of this ticket's attempts; never the conversation. */
  runChanged(runId: string): void {
    if (this.snap.detail.data?.attempts.some((a) => a.runId === runId)) this.ticketTick.within(TICKET_READ_MS);
  }

  /** (d) a SpecCutReviewed in the live attempt's stream. */
  cutReviewed(): void {
    this.conversationTick.within(TICKET_READ_MS);
  }

  /** A view shows this attempt's per-run read: fetched once when terminal, kept fresh while live. */
  want(kind: PerRunRead, runId: string): void {
    const key = `${kind}:${runId}`;
    this.wanted.add(key);
    const held = this.perRun(kind)[runId];
    if (held && !held.failed) return;
    this.readPerRun(kind, runId);
  }

  private perRun(kind: PerRunRead): Readonly<Record<string, Read<unknown>>> {
    return kind === "series" ? this.snap.runSeries : this.snap.runPasses;
  }

  private readTicket(): void {
    void this.read("detail", () => this.api.detail(this.project, this.ticketId, this.abort.signal),
      (detail) => this.detailLanded(detail));
    void this.read("series", () => this.api.series(this.project, this.ticketId, null, this.abort.signal));
    for (const key of this.wanted) {
      const [kind, runId] = splitKey(key);
      if (this.isLive(runId)) this.readPerRun(kind, runId);
    }
  }

  private readConversation(): void {
    void this.read("conversation", () => this.api.conversation(this.project, this.ticketId, this.abort.signal));
  }

  /** (c) a changed attempt status — a park, a hand-back, an end — re-reads the conversation, and a
   *  per-run read of an attempt whose status moved once more, so its last passes land. */
  private detailLanded(detail: TicketDetail): void {
    const statuses = detail.attempts.map((a) => `${a.runId}=${a.status}`).join(",");
    const before = this.lastStatuses;
    this.lastStatuses = statuses;
    if (before === null || before === statuses) return;
    this.conversationTick.within(TICKET_READ_MS);
    const moved = detail.attempts.filter((a) => !before.split(",").includes(`${a.runId}=${a.status}`));
    for (const attempt of moved) {
      for (const kind of ["series", "passes"] as const) {
        if (this.wanted.has(`${kind}:${attempt.runId}`) && !isRunLive(attempt.status)) this.readPerRun(kind, attempt.runId);
      }
    }
  }

  private isLive(runId: string): boolean {
    const attempt = this.snap.detail.data?.attempts.find((a) => a.runId === runId);
    return attempt !== undefined && isRunLive(attempt.status);
  }

  private readPerRun(kind: PerRunRead, runId: string): void {
    const key = `${kind}:${runId}`;
    if (this.inFlight.has(key)) {
      this.again.add(key);
      return;
    }
    this.inFlight.add(key);
    const generation = this.generation;
    const load = kind === "series"
      ? () => this.api.series(this.project, this.ticketId, runId, this.abort.signal)
      : () => this.api.passes(runId, this.abort.signal);
    const field = kind === "series" ? "runSeries" : "runPasses";
    if (!this.snap[field][runId]) this.publish({ ...this.snap, [field]: { ...this.snap[field], [runId]: PENDING } });
    void load()
      .then((data) => this.landed(generation, field, runId, { data, failed: false, refusal: null, loading: false }))
      .catch((thrown: unknown) => this.landed(generation, field, runId, failure<unknown>(thrown, this.snap[field][runId])))
      .finally(() => {
        if (generation !== this.generation) return;
        this.inFlight.delete(key);
        if (this.again.delete(key) && !this.disposed) this.readPerRun(kind, runId);
      });
  }

  private landed(generation: number, field: "runSeries" | "runPasses", runId: string, read: Read<unknown>): void {
    if (this.disposed || generation !== this.generation) return;
    this.publish({ ...this.snap, [field]: { ...this.snap[field], [runId]: read } });
  }

  private async read<K extends "detail" | "series" | "conversation">(
    field: K, load: () => Promise<NonNullable<TicketPageSnapshot[K]["data"]>>, after?: (data: never) => void,
  ): Promise<void> {
    const generation = this.generation;
    try {
      const data = await load();
      if (this.disposed || generation !== this.generation) return;
      this.publish({ ...this.snap, [field]: { data, failed: false, refusal: null, loading: false } });
      after?.(data as never);
    } catch (thrown) {
      if (this.disposed || generation !== this.generation) return;
      this.publish({ ...this.snap, [field]: failure<unknown>(thrown, this.snap[field]) });
    }
  }

  private publish(next: TicketPageSnapshot): void {
    this.snap = next;
    this.onChange(next);
  }
}

function splitKey(key: string): [PerRunRead, string] {
  const at = key.indexOf(":");
  return [key.slice(0, at) as PerRunRead, key.slice(at + 1)];
}

/** A failed re-read keeps what an earlier read showed; only the flags say it failed. */
function failure<T>(thrown: unknown, held: Read<T> | undefined): Read<T> {
  const refusal = refusalIn(thrown);
  const notFound = thrown instanceof ApiResponseError && thrown.status === 404;
  return { data: held?.data ?? null, failed: refusal === null, refusal, loading: false, notFound };
}
