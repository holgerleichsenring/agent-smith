"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { RUN_END_KINDS, type TicketAttemptMark } from "@/types/tickets";
import { causeWords } from "./ticketFormat";

// 2026-10-13-9a59p: the mock's .attempts — one mark per attempt, oldest first. An attempt a reaper
// ended (endedOnInfrastructure) is drawn stopped, never failed: the work did not fail, the
// machine under it did.
// 2026-11-22-50d7a: each mark says what it is — "Attempt 3 · restarted by Ada · delivered" — as its title and its
// accessible name, and the group says that it is one square per attempt.

export type MarkKind = "handed-back" | "asked" | "failed" | "shortfall" | "ok" | "running" | "queued" | "stopped";

const MARK_CLASS: Record<MarkKind, string> = {
  "handed-back": "hb",
  asked: "hb",
  failed: "bad",
  // 2026-11-22-7103a: a shortfall stopped before its contract — red like the run's status line, its own kind and title.
  shortfall: "bad",
  ok: "ok",
  running: "run",
  queued: "",
  stopped: "stop",
};

export function markKind(mark: TicketAttemptMark): MarkKind {
  if (mark.handedBack) return "handed-back";
  if (mark.endedOnInfrastructure) return "stopped";
  switch (mark.status) {
    case "success":
      return "ok";
    case "shortfall":
      return "shortfall";
    case "failed":
      return "failed";
    case "running":
      return "running";
    case "queued":
      return "queued";
    case "waiting_for_input":
      return "asked";
    default:
      return "stopped";
  }
}

const END_KINDS: ReadonlySet<string> = new Set(Object.values(RUN_END_KINDS));

type EndKindKey = `tickets.endKind.${(typeof RUN_END_KINDS)[keyof typeof RUN_END_KINDS]}`;

/** How the attempt ended, with the classified reason where its run recorded one: "failed: a precondition did not hold". */
export function markOutcome(t: T, mark: TicketAttemptMark): string {
  const kind = markKind(mark);
  const reaped = mark.endedOnInfrastructure && !mark.handedBack;
  const outcome = reaped ? t("tickets.outcome.infrastructure") : t(`tickets.outcome.${kind}`);
  if (!mark.endKind || !END_KINDS.has(mark.endKind)) return outcome;
  return t("tickets.outcome.because", { outcome, reason: t(`tickets.endKind.${mark.endKind}` as EndKindKey) });
}

/** One mark's description: its number, how it began and by whom where recorded, how it ended. */
export function markTitle(t: T, mark: TicketAttemptMark, number: number): string {
  const parts = [t("tickets.marks.attempt", { n: number })];
  if (mark.cause) parts.push(causeWords(t, mark.cause));
  parts.push(markOutcome(t, mark));
  return parts.join(" · ");
}

/** 2026-11-22-9719b: the most marks a row draws; older ones fold into "+N" so a long history keeps its column. */
export const MAX_MARKS = 6; // the marks column is 76px wide

export function TicketAttemptMarks({ marks, count }: { marks: TicketAttemptMark[]; count: number }) {
  const t = useT();
  const shown = marks.slice(-MAX_MARKS);
  const folded = marks.length - shown.length;
  const label = folded > 0
    ? t("tickets.marks.labelFolded", { count, shown: shown.length, folded })
    : t("tickets.marks.label", { count });
  return (
    <div className="attempts hidesm" data-testid="ticket-attempts" role="group" aria-label={label} title={label}>
      {folded > 0 && (
        <span className="al-more" title={t("tickets.marks.folded", { count: folded })} data-testid="ticket-attempts-folded">+{folded}</span>
      )}
      {shown.map((mark, i) => {
        const kind = markKind(mark);
        const title = markTitle(t, mark, folded + i + 1);
        return <i key={mark.runId} className={MARK_CLASS[kind]} data-mark={kind} title={title} aria-label={title} role="img" />;
      })}
      <span className="al" aria-hidden="true">{count}</span>
    </div>
  );
}
