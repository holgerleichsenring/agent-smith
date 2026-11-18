import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-17-d6fbb: the run's facts in words — what the graph view's side rail showed as metric tiles, said as the
// run log's meta and facts lines. Each is null where the run recorded nothing, so nothing is drawn for it.

const TERMINAL = new Set(["success", "shortfall", "failed", "error", "cancelled"]);

export function isTerminal(snapshot: RunSnapshot): boolean {
  return TERMINAL.has((snapshot.status ?? "").toLowerCase());
}

/** "14m", "43s". */
export function shortMs(ms: number): string {
  const seconds = Math.round(ms / 1000);
  return seconds < 60 ? `${seconds}s` : `${Math.round(seconds / 60)}m`;
}

/** The share of the cap spent, 0..100, and whether it is near or over; null without a cap. */
export function budgetShare(snapshot: RunSnapshot): { percent: number; tone: "ok" | "near" | "over" } | null {
  const cap = snapshot.budgetCapUsd;
  if (cap == null || cap <= 0) return null;
  const share = snapshot.costUsd / cap;
  return { percent: Math.min(100, Math.round(share * 100)), tone: share >= 1 ? "over" : share > 0.8 ? "near" : "ok" };
}

export interface PodLine {
  name: string;
  image: string;
  resources: string;
}

/** The pods the run spawned, or while none has landed on a run that will have some, the reservation. */
export function computeOf(snapshot: RunSnapshot): { words: string; heading: string; pods: PodLine[] } | null {
  const live = snapshot.liveCompute?.pods ?? [];
  if (live.length > 0) {
    return {
      words: `${live.length} ${live.length === 1 ? "pod" : "pods"}, ${snapshot.liveCompute?.totalMem ?? "memory unknown"}`,
      heading: "The pods this run spawned",
      pods: live.map((p) => ({ name: p.repo, image: p.image, resources: p.mem })),
    };
  }
  const reserved = snapshot.footprint?.pods ?? [];
  if (isTerminal(snapshot) || !snapshot.footprint) return null;
  return {
    words: "compute calculating",
    heading: "Reserved at admission, live pods pending",
    pods: reserved.map((p) => ({ name: p.repo, image: p.image, resources: `${p.memLimit}, ${p.cpuLimit}` })),
  };
}

/** "6m model, 2m sandbox, 1m scaffolding, 30s throttled", or the older model-only split. */
export function timeSplitWords(snapshot: RunSnapshot): string | null {
  const split = snapshot.timeSplit;
  if (split) {
    const parts = [`${shortMs(split.modelMs)} model`, `${shortMs(split.sandboxMs)} sandbox`, `${shortMs(split.scaffoldingMs ?? 0)} scaffolding`];
    if (split.throttleMs > 0) parts.push(`${shortMs(split.throttleMs)} throttled`);
    return parts.join(", ");
  }
  const llm = snapshot.llmDurationMs ?? 0;
  if (llm <= 0) return null;
  const throttle = snapshot.throttleWaitMs ?? 0;
  return throttle > 0 ? `${shortMs(llm)} model, ${shortMs(throttle)} throttled` : `${shortMs(llm)} model`;
}
