import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { HubGroupRegistry } from "./HubGroupRegistry";
import { HubReconnectPolicy } from "./HubReconnectPolicy";
import type {
  RunEvent,
  SandboxActivityRollup,
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

  // p0366: the live rejoin thunk for every group this client is currently a
  // member of, keyed the same way as the ref-count registry. A SignalR
  // automatic-reconnect gets a NEW ConnectionId, so every server-side group
  // membership from the old connection is gone; on reconnected we replay these
  // thunks to rejoin. Registered on the FIRST subscriber (0→1) and dropped on
  // the LAST unsubscribe (1→0), so the map always mirrors the active groups.
  private readonly rejoiners = new Map<string, () => Promise<unknown>>();

  // p0246f: the overview run list lives in the DB system-of-record — the
  // dashboard fetches it via runsApi and refetches when this nudge fires. The
  // nudge carries only the changed runId (transport, not data); there is no
  // client-side fold or cache of run state anymore. p0225's behavior-subject
  // replay survives only for the snapshot streams that are still live KPIs
  // (systemActivityUpdates), where a late subscriber genuinely needs the last
  // value without a refresh.
  readonly runsChanged = makeSubject<string>();
  readonly runEvents = makeSubject<{ runId: string; event: RunEvent }>();
  readonly sandboxEvents = makeSubject<{ runId: string; repo: string; event: RunEvent }>();
  readonly systemEvents = makeSubject<SystemEvent>();
  // p0248: the one-shot backfill on SubscribeSystem arrives as a single array so
  // the dashboard seeds it in one render instead of stepping through it.
  readonly systemBacklog = makeSubject<SystemEvent[]>();
  readonly systemActivityUpdates = makeBehaviorSubject<SystemActivitySnapshot>();
  // p0370: the coalesced sandbox-activity beat (p0367) that replaced the Run-group
  // tool-call firehose — one rollup per run per interval, feeds the detail liveness.
  readonly sandboxActivity = makeSubject<SandboxActivityRollup>();
  readonly connectionState = makeSubject<HubConnectionState>();

  constructor(options: JobsHubClientOptions) {
    this.options = options;
  }

  state(): HubConnectionState {
    return this.connection?.state ?? HubConnectionState.Disconnected;
  }

  // p0366: register a group's rejoin thunk on the first subscriber and issue
  // the initial invoke; a re-invoke on reconnect uses the same thunk (which
  // reads this.connection at call time — automatic-reconnect keeps the same
  // HubConnection instance, only the ConnectionId changes). Idempotent: a
  // repeated SubscribeRun/SubscribeSystem replays the retained window, which
  // the ScopeBuffers dedup against the live tail.
  private async join(key: string, invoke: () => Promise<unknown>): Promise<void> {
    if (this.groups.incRef(key)) {
      this.rejoiners.set(key, invoke);
      await invoke();
    }
  }

  /** Drops the last subscriber's rejoin thunk; returns true on 1→0. */
  private leave(key: string): boolean {
    if (this.groups.decRef(key)) {
      this.rejoiners.delete(key);
      return true;
    }
    return false;
  }

  async subscribeOverview(): Promise<() => Promise<void>> {
    await this.ensureStarted();
    await this.join(KEY_OVERVIEW, () => this.connection!.invoke("SubscribeOverview"));
    return () => this.unsubscribeOverview();
  }

  private async unsubscribeOverview(): Promise<void> {
    // Hub side has no explicit Unsubscribe — the group is per-connection;
    // closing the connection or letting it idle removes membership. We just
    // stop forwarding to listeners and drop the rejoin thunk.
    this.leave(KEY_OVERVIEW);
  }

  async subscribeRun(runId: string): Promise<() => Promise<void>> {
    await this.ensureStarted();
    await this.join(keyRun(runId), () => this.connection!.invoke("SubscribeRun", runId));
    return () => this.unsubscribeRun(runId);
  }

  private async unsubscribeRun(runId: string): Promise<void> {
    this.leave(keyRun(runId));
  }

  /**
   * p0173a: subscribes the caller to the system event group. Identical
   * shape to subscribeOverview — system events are global, no per-run
   * scoping. Replays the retained system stream window before live tail
   * starts (the hub does the XRANGE replay server-side).
   */
  async subscribeSystem(): Promise<() => Promise<void>> {
    await this.ensureStarted();
    await this.join(KEY_SYSTEM, () => this.connection!.invoke("SubscribeSystem"));
    return () => this.unsubscribeSystem();
  }

  private async unsubscribeSystem(): Promise<void> {
    this.leave(KEY_SYSTEM);
  }

  async expandSandbox(runId: string, repo: string): Promise<() => Promise<void>> {
    await this.ensureStarted();
    await this.join(
      keySandbox(runId, repo),
      () => this.connection!.invoke("ExpandSandbox", runId, repo),
    );
    return () => this.collapseSandbox(runId, repo);
  }

  private async collapseSandbox(runId: string, repo: string): Promise<void> {
    if (this.leave(keySandbox(runId, repo))) {
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

  /**
   * p0235: fetches the run's plan.md from the artifact-store cache (24h TTL).
   * For coding presets this is the agent's own plan. Null when the run is
   * unknown, the cache has expired, or no plan was written.
   */
  async getPlanMarkdown(runId: string): Promise<string | null> {
    await this.ensureStarted();
    return this.connection!.invoke<string | null>("GetPlanMarkdown", runId);
  }

  /**
   * p0243: fetches the run's analyze.md from the artifact-store cache (24h TTL)
   * — the analyzer's ProjectMap rendered as markdown. Null when the run is
   * unknown, the cache has expired, or no analysis was cached.
   */
  async getAnalyzeMarkdown(runId: string): Promise<string | null> {
    await this.ensureStarted();
    return this.connection!.invoke<string | null>("GetAnalyzeMarkdown", runId);
  }

  async stop(): Promise<void> {
    this.groups.reset();
    this.rejoiners.clear();
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

    // p0246f: a thin nudge — "run {id} changed, refetch from the DB". The
    // dashboard re-fetches GET /api/runs; the hub no longer ships run snapshots.
    conn.on("RunsChanged", (runId: string) => this.runsChanged.emit(runId));
    conn.on("RunEvent", (event: RunEvent) =>
      this.runEvents.emit({ runId: event.runId, event }));
    conn.on("SandboxEvent", (event: RunEvent) => {
      const repo = "repo" in event ? (event as { repo: string }).repo : "";
      this.sandboxEvents.emit({ runId: event.runId, repo, event });
    });
    conn.on("SystemEvent", (event: SystemEvent) =>
      this.systemEvents.emit(event));
    conn.on("SystemBacklog", (events: SystemEvent[]) =>
      this.systemBacklog.emit(events));
    conn.on("SandboxActivity", (rollup: SandboxActivityRollup) =>
      this.sandboxActivity.emit(rollup));
    conn.on("SystemActivityUpdated", (snapshot: SystemActivitySnapshot) =>
      this.systemActivityUpdates.emit(snapshot));
    conn.onreconnecting(() => this.connectionState.emit(HubConnectionState.Reconnecting));
    // p0366: the reconnected connection has a fresh ConnectionId, so it belongs
    // to no groups. Rejoin every active subscription BEFORE announcing Connected
    // so a consumer reacting to the Connected transition never sees a live-but-
    // empty view. Connected is emitted even if a rejoin invoke rejects (the
    // transport IS up) — a still-transitioning drop re-enters Reconnecting.
    conn.onreconnected(() =>
      void this.rejoinAll().finally(() =>
        this.connectionState.emit(HubConnectionState.Connected)));
    conn.onclose(() => this.connectionState.emit(HubConnectionState.Disconnected));

    this.connection = conn;
    this.connectionState.emit(HubConnectionState.Connecting);
    await conn.start();
    this.connectionState.emit(HubConnectionState.Connected);
  }

  // p0366: re-invoke every active group's rejoin thunk on a fresh connection.
  // Best-effort per thunk — one failing rejoin must not block the others, and a
  // reject only means the connection dropped again (the next reconnect retries).
  private async rejoinAll(): Promise<void> {
    const invokes = [...this.rejoiners.values()].map((invoke) =>
      invoke().catch(() => { /* connection re-transitioning; next reconnect retries */ }));
    await Promise.all(invokes);
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
