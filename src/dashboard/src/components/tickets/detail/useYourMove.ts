"use client";

import { useLiveAttempt, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import { ticketState, type TicketStateView } from "./ticketState";
import { yourMove, type YourMove } from "./yourMove";

// 2026-11-18-d6d2d: the Your move box's state and move, read once for the box and for the thread — the newest
// attempt's report leaves Retry to the box when the box's one act already is that Retry.

export function useYourMove(detail: TicketDetail | null): { view: TicketStateView; move: YourMove } | null {
  const live = useLiveAttempt();
  const series = useTicketSeries().data;
  const view = detail ? ticketState({ detail, liveEvents: live.events, series }) : null;
  if (!detail || !view) return null;
  return { view, move: yourMove(view, detail.summary.bucket, detail.summary.kind, view.attempt?.endDetail) };
}
