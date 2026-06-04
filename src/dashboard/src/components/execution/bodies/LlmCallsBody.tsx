"use client";

import type { PairedLlmCall } from "@/hooks/execution-tree/llmPairing";

// p0203: renders the per-step paired LLM rows. One line per call,
// showing role, model, duration, tokens, $cost, cache-hit. Role
// "unknown" surfaces a visual marker — the producer fix to thread the
// real skill name through every callsite is sliced to p0203a.

interface LlmCallsBodyProps {
  calls: ReadonlyArray<PairedLlmCall>;
  /** p0227: true once the run is terminal (success/failed/canceled). An
   *  unfinished call on an ended run was cut off, not still in flight. */
  runEnded?: boolean;
}

export function LlmCallsBody({ calls, runEnded = false }: LlmCallsBodyProps) {
  if (calls.length === 0) return null;
  return (
    <div data-testid="llm-calls-body" className="space-y-1">
      {calls.map((c) => (
        <LlmCallRow key={c.id} call={c} runEnded={runEnded} />
      ))}
    </div>
  );
}

function LlmCallRow({ call, runEnded }: { call: PairedLlmCall; runEnded: boolean }) {
  return (
    <div
      data-testid={`llm-call-${call.id}`}
      data-paired={call.finishedAt !== null}
      data-role-unknown={call.roleIsUnknown}
      className="flex items-center gap-2 font-mono dsh-label text-stone-700"
    >
      <RoleLabel role={call.role} unknown={call.roleIsUnknown} phase={call.phase} />
      <span className="text-stone-500">{call.model}</span>
      <span className="text-stone-400">{formatDuration(call.durationMs)}</span>
      <span className="text-stone-400">{formatTokens(call.tokensIn, call.tokensOut)}</span>
      <span className="text-stone-700">{formatCost(call.costUsd)}</span>
      {call.cacheHit && (
        <span
          data-testid={`llm-call-${call.id}-cache-hit`}
          className="rounded bg-blue-100 px-1.5 py-0.5 dsh-label text-blue-800"
        >
          cache hit
        </span>
      )}
      {call.finishedAt === null && (
        runEnded ? (
          // p0227: the run stopped before this call reported back — it was cut
          // off, not running. Render neutral "ended", not a pulsing in-flight.
          <span className="rounded bg-stone-100 px-1.5 py-0.5 dsh-label text-stone-500">
            ended
          </span>
        ) : (
          <span className="rounded bg-amber-100 px-1.5 py-0.5 dsh-label text-amber-800">
            in flight
          </span>
        )
      )}
    </div>
  );
}

function RoleLabel({ role, unknown, phase }: { role: string; unknown: boolean; phase: string | null }) {
  if (unknown) {
    // p0222: never render a bare "unknown" activity. Fall back to the turn's
    // phase, then a generic label, so every LLM turn carries an activity label.
    // (The producer-side fix that threads the real skill role through every
    // callsite is the separately-tracked p0203a.)
    const label = phase && phase.length > 0 ? phase : "llm call";
    return (
      <span
        data-testid="llm-call-activity"
        className="font-semibold text-stone-600"
        title="Producer role not threaded (p0203a) — showing the call phase"
      >
        {label}
      </span>
    );
  }
  return <span data-testid="llm-call-activity" className="font-semibold text-stone-800">{role}</span>;
}

function formatDuration(ms: number | null): string {
  if (ms === null) return "—";
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatTokens(input: number | null, output: number | null): string {
  if (input === null || output === null) return "—";
  return `${input}in/${output}out`;
}

function formatCost(usd: number | null): string {
  if (usd === null) return "—";
  return `$${usd.toFixed(4)}`;
}
