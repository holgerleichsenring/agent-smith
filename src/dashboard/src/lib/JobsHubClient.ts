import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { HubGroupRegistry } from "./HubGroupRegistry";
import { HubReconnectPolicy } from "./HubReconnectPolicy";
import type {
  OverviewSnapshot,
  RunEvent,
  RunSnapshot,
  SystemActivitySnapshot,
} from "@/types/hub-events";
import type { SystemEvent } from "@/types/system-events";

// p0169f: single shared HubConnection per tab; ref-counted group
// subscriptions; lazy-connect on first subscribe. Owns the connection
// lifecycle so React components can call subscribeOverview / subscribeRun
// / expandSandbox without worrying about the underlying transport.

type Listener<T> = (value: T) => void;

interface SubjectMap<T> {
  add(listener: Listener<T>): () => void;
  emit(value: T): void;
}

function makeSubject<T>(): SubjectMap<T> {
  const listeners = new Set<Listener<T>>();
  return {
    add(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    emit(value) {
      for (const listener of listeners) listener(value);
    },
  };
}

// p0225: a subject that remembers its last emitted value and replays it to any
// listener that subscribes LATER. State/snapshot streams need this — AppRail
// calls useJobsHub() and holds the overview subscription for the app's whole
// lifetime, so the hub's one-time SubscribeOverview snapshot is pushed once;
// a later-mounting consumer (RunsList on a client-side nav) would otherwise
// register its listener too late and stay empty until a hard refresh. Replaying
// the cached snapshot on subscribe fixes that. Event streams stay plain
// makeSubject — replaying a single stale event would be wrong.
export function makeBehaviorSubject<T>(): SubjectMap<T> {
  const listeners = new Set<Listener<T>>();
  let last: { value: T } | null = null;
  return {
    add(listener) {
      listeners.add(listener);
      if (last) listener(last.value);
      return () => listeners.delete(listener);
    },
    emit(value) {
      last = { value };
      for (const listener of listeners) listener(value);
    },
  };
}

const KEY_OVERVIEW = "overview";
const KEY_SYSTEM = "system";
const keyRun = (runId: string) => `run:${runId}`;
const keySandbox = (runId: string, repo: string) => `sandbox:${runId}:${repo}`;

export interface JobsHubClientOptions {
  hubUrl: string;
}

export class JobsHubClient {
  private readonly options: JobsHubClientOptions;
  private readonly groups = new HubGroupRegistry();
  private connection: HubConnection | null = null;
  private startPromise: Promise<void> | null = null;

  // p0225: snapshot streams replay their last value to late subscribers (see
  // makeBehaviorSubject) so a component mounting after AppRail still gets the
  // current overview / system activity without a refresh.
  readonly overviewSnapshots = makeBehaviorSubject<OverviewSnapshot>();
  readonly jobUpserts = makeSubject<RunSnapshot>();
  // p0233: the client folds JobUpserted events into this authoritative overview
  // and re-emits the WHOLE snapshot, so the behavior subject always carries the
  // CURRENT state. Previously each useJobsHub instance folded upserts itself —
  // a list that remounted (navigate away + back) replayed only the stale
  // first-subscribe snapshot and missed every upsert that arrived while it was
  // unmounted, so a new job never showed until a full page reload.
  private overviewCache: OverviewSnapshot | null = null;
  readonly runEvents = makeSubject<{ runId: string; event: RunEvent }>();
  readonly sandboxEvents = makeSubject<{ runId: string; repo: string; event: RunEvent }>();
  readonly systemEvents = makeSubject<SystemEvent>();
  readonly systemActivityUpdates = makeBehaviorSubject<SystemActivitySnapshot>();
  readonly connectionState = makeSubject<HubConnectionState>();

  constructor(options: JobsHubClientOptions) {
    this.options = options;
  }

  state(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }

  async subscribeOverview(): Promise<() => Promise<void>> {
    await this.ensureStarted();
    const key = KEY_OVERVIEW;
    if (this.groups.incRef(key)) {
      await this.connection!.invoke("SubscribeOverview");
    }
    return () => this.unsubscribeOverview();
  }

  private async unsubscribeOverview(): Promise<void> {
    if (this.groups.decRef(KEY_OVERVIEW)) {
      // Hub side has no explicit Unsubscribe — the group is per-connection;
      // closing the connection or letting it idle removes membership. We
      // just stop forwarding to listeners.
    }
  }

  async subscribeRun(runId: string): Promise<() => Promise<void>> {
    await this.ensureStarted();
    const key = keyRun(runId);
    if (this.groups.incRef(key)) {
      await this.connection!.invoke("SubscribeRun", runId);
    }
    return () => this.unsubscribeRun(runId);
  }

  private async unsubscribeRun(runId: string): Promise<void> {
    this.groups.decRef(keyRun(runId));
  }

  /**
   * p0173a: subscribes the caller to the system event group. Identical
   * shape to subscribeOverview — system events are global, no per-run
   * scoping. Replays the retained system stream window before live tail
   * starts (the hub does the XRANGE replay server-side).
   */
  async subscribeSystem(): Promise<() => Promise<void>> {
    await this.ensureStarted();
    const key = KEY_SYSTEM;
    if (this.groups.incRef(key)) {
      await this.connection!.invoke("SubscribeSystem");
    }
    return () => this.unsubscribeSystem();
  }

  private async unsubscribeSystem(): Promise<void> {
    this.groups.decRef(KEY_SYSTEM);
  }

  async expandSandbox(runId: string, repo: string): Promise<() => Promise<void>> {
    await this.ensureStarted();
    const key = keySandbox(runId, repo);
    if (this.groups.incRef(key)) {
      await this.connection!.invoke("ExpandSandbox", runId, repo);
    }
    return () => this.collapseSandbox(runId, repo);
  }

  private async collapseSandbox(runId: string, repo: string): Promise<void> {
    const key = keySandbox(runId, repo);
    if (this.groups.decRef(key)) {
      try { await this.connection?.invoke("CollapseSandbox", runId, repo); }
      catch { /* hub may be transitioning; safe to swallow */ }
    }
  }

  /** p0169h: fetches the full retained event window for the trail tab. */
  async getTrail(runId: string): Promise<RunEvent[]> {
    await this.ensureStarted();
    return this.connection!.invoke<RunEvent[]>("GetTrail", runId);
  }

  /**
   * p0169j-c: fetches the rendered result.md from the server artifact store
   * cache (24h TTL). Returns null when the run is unknown, the cache has
   * expired, or WriteRunResult hasn't fired yet for an in-flight run.
   */
  async getResultMarkdown(runId: string): Promise<string | null> {
    await this.ensureStarted();
    return this.connection!.invoke<string | null>("GetResultMarkdown", runId);
  }

  async stop(): Promise<void> {
    this.groups.reset();
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.startPromise = null;
      this.connectionState.emit(HubConnectionState.Disconnected);
    }
  }

  private async ensureStarted(): Promise<void> {
    if (this.connection && this.connection.state === HubConnectionState.Connected) return;
    if (this.startPromise) return this.startPromise;
    this.startPromise = this.openConnection();
    try { await this.startPromise; }
    finally { /* keep promise to dedupe concurrent ensureStarted */ }
  }

  private async openConnection(): Promise<void> {
    const conn = new HubConnectionBuilder()
      .withUrl(this.options.hubUrl)
      .withAutomaticReconnect(new HubReconnectPolicy())
      .configureLogging(LogLevel.Warning)
      .build();

    conn.on("OverviewSnapshot", (snapshot: OverviewSnapshot) => {
      this.overviewCache = snapshot;
      this.overviewSnapshots.emit(snapshot);
    });
    conn.on("JobUpserted", (snapshot: RunSnapshot) => {
      // Fold into the cached overview and re-emit the whole thing, so a
      // late/remounting subscriber gets the current list via the behavior
      // subject's replay. jobUpserts stays for any single-event consumer.
      this.overviewCache = foldOverviewUpsert(this.overviewCache, snapshot);
      this.overviewSnapshots.emit(this.overviewCache);
      this.jobUpserts.emit(snapshot);
    });
    conn.on("RunEvent", (event: RunEvent) =>
      this.runEvents.emit({ runId: event.runId, event }));
    conn.on("SandboxEvent", (event: RunEvent) => {
      const repo = "repo" in event ? (event as { repo: string }).repo : "";
      this.sandboxEvents.emit({ runId: event.runId, repo, event });
    });
    conn.on("SystemEvent", (event: SystemEvent) =>
      this.systemEvents.emit(event));
    conn.on("SystemActivityUpdated", (snapshot: SystemActivitySnapshot) =>
      this.systemActivityUpdates.emit(snapshot));
    conn.onreconnecting(() => this.connectionState.emit(HubConnectionState.Reconnecting));
    conn.onreconnected(() => this.connectionState.emit(HubConnectionState.Connected));
    conn.onclose(() => this.connectionState.emit(HubConnectionState.Disconnected));

    this.connection = conn;
    this.connectionState.emit(HubConnectionState.Connecting);
    await conn.start();
    this.connectionState.emit(HubConnectionState.Connected);
  }
}

let singleton: JobsHubClient | null = null;
export function getJobsHubClient(hubUrl: string): JobsHubClient {
  if (!singleton) singleton = new JobsHubClient({ hubUrl });
  return singleton;
}

/** Test-only: reset the module-level singleton between tests. */
export function __resetJobsHubClientForTests(): void {
  singleton = null;
}

// p0233: fold one JobUpserted into the overview. A running run is upserted into
// `active` (by runId); a terminal run moves to the front of `recent`. UI-level
// concerns (pre-spawn zombie hiding, display caps, debug mode) stay in
// useJobsHub.applySnapshotFilters, applied on every emit — this keeps the raw,
// authoritative list. Exported for unit tests.
const RECENT_FOLD_CAP = 100;
export function foldOverviewUpsert(
  current: OverviewSnapshot | null,
  snapshot: RunSnapshot,
): OverviewSnapshot {
  const base = current ?? { active: [], recent: [], systemActivity: null };
  const isTerminal = ["success", "failed", "error"].includes(snapshot.status.toLowerCase());
  if (isTerminal) {
    return {
      active: base.active.filter((r) => r.runId !== snapshot.runId),
      recent: [snapshot, ...base.recent.filter((r) => r.runId !== snapshot.runId)].slice(0, RECENT_FOLD_CAP),
      systemActivity: base.systemActivity,
    };
  }
  const idx = base.active.findIndex((r) => r.runId === snapshot.runId);
  const active = idx >= 0
    ? base.active.map((r, i) => (i === idx ? snapshot : r))
    : [snapshot, ...base.active];
  return { active, recent: base.recent, systemActivity: base.systemActivity };
}
