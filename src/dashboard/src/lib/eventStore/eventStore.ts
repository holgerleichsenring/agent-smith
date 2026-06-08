import type { RunEvent } from "@/types/hub-events";
import type { SystemEvent } from "@/types/system-events";
import { ScopeBuffer } from "./scopeBuffer";

// p0218: shared scoped event store. Holds ONE system backlog (all subsystems,
// fed by a single subscribeSystem) and one backlog per run, each as a
// ScopeBuffer. Views select their scope and read the persistent backlog instead
// of each opening their own firehose + clearing it on mount. No backend stream
// change — this is purely the client-side subscription-scoping fix.

// Caps mirror the prior per-hook windows (JobsBroadcaster's SystemRecent ring
// is 500; the per-run log capped at 2000).
const SYSTEM_CAP = 500;
const RUN_CAP = 2000;

// Minimal slice of JobsHubClient the store depends on — inverts the dependency
// so the store is unit-testable with a fake source.
export interface HubEventSource {
  systemEvents: { add(listener: (event: SystemEvent) => void): () => void };
  // p0248: the one-shot backfill batch from SubscribeSystem (seeded in one go).
  systemBacklog: { add(listener: (events: SystemEvent[]) => void): () => void };
  runEvents: { add(listener: (entry: { runId: string; event: RunEvent }) => void): () => void };
  subscribeSystem(): Promise<() => Promise<void>>;
  subscribeRun(runId: string): Promise<() => Promise<void>>;
}

export class EventStore {
  private readonly system: ScopeBuffer<SystemEvent>;
  private readonly runs = new Map<string, ScopeBuffer<RunEvent>>();

  constructor(private readonly source: HubEventSource) {
    this.system = new ScopeBuffer<SystemEvent>(
      SYSTEM_CAP,
      (push, pushMany) => {
        const offLive = source.systemEvents.add(push);
        const offBatch = source.systemBacklog.add(pushMany);
        return source.subscribeSystem().then((cancel) => async () => {
          offLive();
          offBatch();
          await cancel();
        });
      },
      // SubscribeSystem replays the full retained window on every (re)subscribe;
      // key on the serialized event so a reconnect / remount re-replay is a
      // no-op instead of duplicating the whole tracker history. Each event
      // carries a timestamp, so distinct events never collide.
      (event) => JSON.stringify(event),
    );
  }

  systemScope(): ScopeBuffer<SystemEvent> {
    return this.system;
  }

  runScope(runId: string): ScopeBuffer<RunEvent> {
    let scope = this.runs.get(runId);
    if (!scope) {
      scope = new ScopeBuffer<RunEvent>(RUN_CAP, (push) => {
        const off = this.source.runEvents.add(({ runId: emitted, event }) => {
          if (emitted === runId) push(event);
        });
        return this.source.subscribeRun(runId).then((cancel) => async () => {
          off();
          await cancel();
        });
      });
      this.runs.set(runId, scope);
    }
    return scope;
  }
}
