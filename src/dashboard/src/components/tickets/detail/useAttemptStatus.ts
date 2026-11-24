"use client";

import { attemptStatus, latestAttempt, type RunStatusLine, type StatusContext } from "@/components/jobs/runlog/runStatusLine";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { modelWaitHead } from "@/components/jobs/runlog/modelWaitWords";
import { currentModelWait } from "@/components/jobs/runlog/runNowLines";
import { useDateLocale, useT } from "@/hooks/useT";
import { useLiveAttempt, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";
import { RUNS_CONTROL } from "./RetryUnchanged";
import { useYourMove } from "./useYourMove";

// 2026-11-22-7103a: an attempt's status line with its next step — the ticket's Your move for the newest attempt, the way
// to the latest for an older one, Retry named only to a reader who holds runs.control.

/** What every run line of this ticket reads its next step from, and the newest attempt's queue place. */
export function useStatusContext(): { ctx: StatusContext; queuePlace: number | null } {
  const { data, project } = useTicketDetail();
  const detail = data ?? null;
  const move = useYourMove(detail)?.move ?? null;
  const canRetry = !useLacksPermission(RUNS_CONTROL, project);
  return { ctx: { move, latest: latestAttempt(detail), canRetry }, queuePlace: detail?.summary.queuePlace ?? null };
}

export function useAttemptStatus(attempt: TicketAttempt): RunStatusLine {
  const { ctx, queuePlace } = useStatusContext();
  // 2026-11-22-50d7a: the live attempt's model wait, read off its events as the run log reads it.
  const t = useT();
  const locale = useDateLocale();
  const live = useLiveAttempt();
  const wait = live.attempt?.runId === attempt.runId ? currentModelWait(live.events) : null;
  return attemptStatus(t, attempt, ctx, queuePlace, modelWaitHead(t, wait, locale));
}
