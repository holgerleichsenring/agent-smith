import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { HubGroupRegistry } from "./HubGroupRegistry";
import { HubReconnectPolicy } from "./HubReconnectPolicy";
import { currentAccessToken } from "./auth/session";
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

// p0388b: the per-run trail poll is GONE. Its first tick shipped the whole
// structural trail of the run as one JSON per client per page load, and it
// re-fed exactly the per-tool-call events p0367 deliberately kept off the run
// channel — to feed a client-side fold whose output was ~23 rail rows. The rail
// and each step's body are now bounded queries (GET /api/runs/{id}/steps and
// .../steps/{i}/events); SignalR stays the low-frequency meaning channel.

export interface JobsHubClientOptions {
  hubUrl: string;
  /**
   * 2026-08-25-2de1: a FACTORY, never a captured token. SignalR asks again on
   * every reconnect, and a token frozen at connect time is a connection that
   * reconnects forever an hour later without ever saying why. An empty string is
   * the "no token" answer SignalR already understands.
   */
  accessTokenFactory?: () => Promise<string>;
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
    // SubscribeRun primes the low-frequency lifecycle push + the retained
    // structural replay that feeds the live-window views.
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
   * p0390: fetches the run's work spec — the current revision plus its revision
   * list. The content of record is spec.yaml on the ticket branch; this is the
   * run detail's cached copy. Null when the run derived no spec.
   */
  async getSpecMarkdown(runId: string): Promise<string | null> {
    await this.ensureStarted();
    return this.connection!.invoke<string | null>("GetSpecMarkdown", runId);
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

  // 2026-08-28-0f46: the start promise deduplicates CONCURRENT starts and nothing
  // more. Kept past the moment it settles it becomes a latch for the life of the
  // tab: a rejected first start is handed to every later subscribe (and automatic
  // reconnect covers only a connection that once succeeded, so nothing else
  // retries), and a resolved one is handed back for a connection that has since
  // dropped. Dropping it when it settles is what makes a later subscribe open a
  // connection instead of being told about an old one.
  private async ensureStarted(): Promise<void> {
    const state = this.connection?.state;
    if (state === HubConnectionState.Connected) return;
    // A reconnect in flight is SignalR restoring THIS connection; a second one
    // opened beside it would double every group and every event it carries.
    if (state === HubConnectionState.Reconnecting) return;
    if (this.startPromise) return this.startPromise;
    this.startPromise = this.openConnection();
    try { await this.startPromise; }
    finally { this.startPromise = null; }
  }

  // 2026-08-25-2de1: p0503c reads the handshake token off the query string
  // because a browser cannot set an Authorization header on a websocket
  // handshake — which is exactly what SignalR's own accessTokenFactory produces.
  // With no factory this is the builder call the client has always made.
  private withUrl(): HubConnectionBuilder {
    const accessTokenFactory = this.options.accessTokenFactory;
    const builder = new HubConnectionBuilder();
    return accessTokenFactory
      ? builder.withUrl(this.options.hubUrl, { accessTokenFactory })
      : builder.withUrl(this.options.hubUrl);
  }

  private async openConnection(): Promise<void> {
    const conn = this.withUrl()
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
    try {
      await conn.start();
    } catch (failure) {
      // 2026-08-28-0f46: a start that rejected leaves a Disconnected connection
      // assigned, which every later ensureStarted then reads as "there is one".
      // The refusal belongs to the caller; what must not survive it is the
      // half-open object.
      this.connection = null;
      this.connectionState.emit(HubConnectionState.Disconnected);
      throw failure;
    }
    // p0366's rejoin, for the OTHER way a connection is replaced: this is not
    // SignalR's automatic reconnect but a fresh connection opened after one
    // dropped, and it belongs to no group either. Empty on a first connection.
    await this.rejoinAll();
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
  if (!singleton) singleton = new JobsHubClient({ hubUrl, accessTokenFactory: hubAccessToken });
  return singleton;
}

// 2026-08-25-2de1: SignalR appends access_token and the Authorization header only
// for a non-empty value, so an installation with no authority configured makes
// byte-identically the handshake it makes today.
export async function hubAccessToken(): Promise<string> {
  return (await currentAccessToken()) ?? "";
}

/** Test-only: reset the module-level singleton between tests. */
export function __resetJobsHubClientForTests(): void {
  singleton = null;
}
