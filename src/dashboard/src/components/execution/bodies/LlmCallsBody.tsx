"use client";

import type { PairedLlmCall } from "@/hooks/execution-tree/llmPairing";

// p0203: renders the per-step paired LLM rows. One line per call,
// showing role, model, duration, tokens, $cost, cache-hit. Role
// "unknown" surfaces a visual marker — the producer fix to thread the
// real skill name through every callsite is sliced to p0203a.

interface LlmCallsBodyProps {
  calls: ReadonlyArray<PairedLlmCall>;
}

export function LlmCallsBody({ calls }: LlmCallsBodyProps) {
  if (calls.length === 0) return null;
  return (
    <div data-testid="llm-calls-body" className="space-y-1">
      {calls.map((c) => (
        <LlmCallRow key={c.id} call={c} />
      ))}
    </div>
  );
}

function LlmCallRow({ call }: { call: PairedLlmCall }) {
  return (
    <div
      data-testid={`llm-call-${call.id}`}
      data-paired={call.finishedAt !== null}
      data-role-unknown={call.roleIsUnknown}
      className="flex items-center gap-2 font-mono dsh-label text-stone-700"
    >
      <RoleLabel role={call.role} unknown={call.roleIsUnknown} />
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
        <span className="rounded bg-amber-100 px-1.5 py-0.5 dsh-label text-amber-800">
          in flight
        </span>
      )}
    </div>
  );
}

function RoleLabel({ role, unknown }: { role: string; unknown: boolean }) {
  if (unknown) {
    return (
      <span
        data-testid="llm-call-role-unknown"
        className="rounded bg-rose-100 px-1.5 py-0.5 text-rose-800"
        title="Producer did not thread the skill name through this LlmCallStartedEvent — see p0203a"
      >
        unknown
      </span>
    );
  }
  return <span className="font-semibold text-stone-800">{role}</span>;
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
