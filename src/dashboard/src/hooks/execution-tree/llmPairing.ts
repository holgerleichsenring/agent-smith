import { EventType, type RunEvent } from "@/types/hub-events";

// p0203: pair LlmCallStartedEvent + LlmCallFinishedEvent into single
// PairedLlmCall rows. Events don't share a callId today so pairing uses
// the spec-mandated heuristic: ordered timestamp + matching run-id +
// matching role + matching model. Unpaired Starts (still in flight) or
// Finishes (matching Start lost) round-trip as singletons so the renderer
// can fall back to two rows when pairing fails.

export interface PairedLlmCall {
  id: string;
  role: string;
  /** True when Role is the producer-side sentinel "unknown". The UI
   *  surfaces this AS-IS with a marker — producer fix is p0203a. */
  roleIsUnknown: boolean;
  model: string;
  /** p0222: phase from the CallScope (e.g. "Execute"/"Triage") — the activity
   *  label fallback when the producer-side role is still "unknown". */
  phase: string | null;
  startedAt: string;
  finishedAt: string | null;
  durationMs: number | null;
  tokensIn: number | null;
  tokensOut: number | null;
  costUsd: number | null;
  /** p0323: prompt tokens served from the provider cache for this call —
   *  the real producer breadcrumb (null until the finish event arrives). */
  cachedTokensIn: number | null;
  /** p0323: prompt tokens written to the provider cache this call. */
  cacheCreationTokensIn: number | null;
  /** Cache-hit indicator surfaced when the producer's CostTracker
   *  recorded a cached read for this call. Inferred from a tokensIn that
   *  is non-zero but costUsd is suspiciously low — actual cache flag
   *  requires the producer-side breadcrumb (deferred to p0203a). */
  cacheHit: boolean;
}

export interface PairingResult {
  pairs: PairedLlmCall[];
  totalCostUsd: number;
  callCount: number;
}

const UNKNOWN_ROLE = "unknown";

// p0227: LlmCallStarted carries the CONFIGURED model (e.g. "gpt-4.1", from
// assignment.Model — p0224), while LlmCallFinished carries the provider's
// reported model (e.g. "gpt-4.1-2025-04-14"). Pairing must tolerate that: the
// configured name is a prefix of the reported one. Treat "unknown" (a producer
// that didn't thread a model) as a wildcard so those still pair.
function modelsMatch(startModel: string, finishModel: string): boolean {
  if (startModel === finishModel) return true;
  if (startModel === UNKNOWN_ROLE || finishModel === UNKNOWN_ROLE) return true;
  return finishModel.startsWith(startModel) || startModel.startsWith(finishModel);
}

export function pairLlmCalls(events: RunEvent[]): PairingResult {
  const starts = events
    .filter((e) => e.type === EventType.LlmCallStarted)
    .map((e) => e as Extract<RunEvent, { type: EventType.LlmCallStarted }>)
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));
  const finishes = events
    .filter((e) => e.type === EventType.LlmCallFinished)
    .map((e) => e as Extract<RunEvent, { type: EventType.LlmCallFinished }>)
    .sort((a, b) => a.timestamp.localeCompare(b.timestamp));

  const used = new Set<number>();
  const pairs: PairedLlmCall[] = [];
  let totalCost = 0;
  for (const start of starts) {
    const matchIdx = finishes.findIndex(
      (f, i) => !used.has(i) && f.role === start.role && modelsMatch(start.model, f.model)
                && f.timestamp >= start.timestamp,
    );
    if (matchIdx === -1) {
      pairs.push(toUnfinishedPair(start));
      continue;
    }
    used.add(matchIdx);
    const finish = finishes[matchIdx];
    totalCost += finish.costUsd;
    pairs.push(toFullPair(start, finish));
  }
  // Surface orphan finishes too — happens when an old Start was replayed.
  finishes.forEach((f, i) => {
    if (used.has(i)) return;
    totalCost += f.costUsd;
    pairs.push(toOrphanFinish(f));
  });

  return { pairs, totalCostUsd: totalCost, callCount: pairs.length };
}

function toFullPair(
  start: Extract<RunEvent, { type: EventType.LlmCallStarted }>,
  finish: Extract<RunEvent, { type: EventType.LlmCallFinished }>,
): PairedLlmCall {
  return {
    id: `llm-${start.timestamp}-${start.role}-${start.model}`,
    role: start.role,
    roleIsUnknown: start.role === UNKNOWN_ROLE,
    model: start.model,
    phase: start.phase,
    startedAt: start.timestamp,
    finishedAt: finish.timestamp,
    durationMs: finish.durationMs,
    tokensIn: finish.tokensIn,
    tokensOut: finish.tokensOut,
    costUsd: finish.costUsd,
    cachedTokensIn: finish.cachedTokensIn ?? 0,
    cacheCreationTokensIn: finish.cacheCreationTokensIn ?? 0,
    cacheHit: finish.tokensIn > 0 && finish.costUsd === 0,
  };
}

function toUnfinishedPair(
  start: Extract<RunEvent, { type: EventType.LlmCallStarted }>,
): PairedLlmCall {
  return {
    id: `llm-${start.timestamp}-${start.role}-${start.model}`,
    role: start.role,
    roleIsUnknown: start.role === UNKNOWN_ROLE,
    model: start.model,
    phase: start.phase,
    startedAt: start.timestamp,
    finishedAt: null,
    durationMs: null,
    tokensIn: null,
    tokensOut: null,
    costUsd: null,
    cachedTokensIn: null,
    cacheCreationTokensIn: null,
    cacheHit: false,
  };
}

function toOrphanFinish(
  finish: Extract<RunEvent, { type: EventType.LlmCallFinished }>,
): PairedLlmCall {
  return {
    id: `llm-orphan-${finish.timestamp}-${finish.role}-${finish.model}`,
    role: finish.role,
    roleIsUnknown: finish.role === UNKNOWN_ROLE,
    model: finish.model,
    phase: finish.phase,
    startedAt: finish.timestamp,
    finishedAt: finish.timestamp,
    durationMs: finish.durationMs,
    tokensIn: finish.tokensIn,
    tokensOut: finish.tokensOut,
    costUsd: finish.costUsd,
    cachedTokensIn: finish.cachedTokensIn ?? 0,
    cacheCreationTokensIn: finish.cacheCreationTokensIn ?? 0,
    cacheHit: finish.tokensIn > 0 && finish.costUsd === 0,
  };
}
