import type { TicketHold } from "@/types/tickets";

// 2026-11-23-31dch: where a hold sends a person — a parked question of any answerer, and the series page a hold's act
// names: its conversation for a park that asks about the plan, its Merge next for a merge.

/** 2026-11-23-31dch: a parked question holds it — the ticket's own, the plan's or the environment's. */
export function isAsked(hold: TicketHold | null | undefined): boolean {
  return hold?.case === "question" || hold?.case === "question_plan" || hold?.case === "question_environment";
}

/** 2026-11-23-d60d: the pull request a hold's act opens on its host; null for every other act. */
export function holdPullRequestHref(hold: TicketHold | null | undefined): string | null {
  return hold?.act?.kind === "pull_request" ? hold.act.target : null;
}

/** 2026-11-23-31dch: where a hold's row leads — a series' conversation for a plan question, its Merge next, else null. */
export function holdSeriesHref(hold: TicketHold | null | undefined, seriesPath: (id: string) => string): string | null {
  const act = hold?.act;
  if (!act?.target) return null;
  if (act.kind === "merge_next") return `${seriesPath(act.target)}?section=next`;
  return act.kind === "conversation" ? `${seriesPath(act.target)}?section=talk` : null;
}
