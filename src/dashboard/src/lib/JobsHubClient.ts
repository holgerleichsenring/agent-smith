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
} from "@/types/hub-events";

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

const KEY_OVERVIEW = "overview";
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

  readonly overviewSnapshots = makeSubject<OverviewSnapshot>();
  readonly jobUpserts = makeSubject<RunSnapshot>();
  readonly runEvents = makeSubject<{ runId: string; event: RunEvent }>();
  readonly sandboxEvents = makeSubject<{ runId: string; repo: string; event: RunEvent }>();
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

    conn.on("OverviewSnapshot", (snapshot: OverviewSnapshot) =>
      this.overviewSnapshots.emit(snapshot));
    conn.on("JobUpserted", (snapshot: RunSnapshot) =>
      this.jobUpserts.emit(snapshot));
    conn.on("RunEvent", (event: RunEvent) =>
      this.runEvents.emit({ runId: event.runId, event }));
    conn.on("SandboxEvent", (event: RunEvent) => {
      const repo = "repo" in event ? (event as { repo: string }).repo : "";
      this.sandboxEvents.emit({ runId: event.runId, repo, event });
    });
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
