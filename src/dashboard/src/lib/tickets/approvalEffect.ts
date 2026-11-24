import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketApprovalPreview } from "@/types/tickets";

// 2026-10-13-9a59r: ONE table of what approving a ticket-bound proposal does, for the ticket composer and
// Work it out's approval card alike. The page renders 9a59l's effect and 9a59m's start arm as data — one
// label per value — and decides nothing itself; an unknown value is shown as the server spelled it.

// 2026-11-23-5669b: the labels are dictionary keys, worded in the chosen language.
const EFFECT_LABELS: Record<string, TextKey> = {
  "bug-ticket": "tickets.approval.effect.bug-ticket",
  "follow-up-closed": "tickets.approval.effect.follow-up-closed",
  "amend-never-worked": "tickets.approval.effect.amend-never-worked",
  "refused-over-cap": "tickets.approval.effect.refused-over-cap",
  refused: "tickets.approval.effect.refused",
  continue: "tickets.approval.effect.continue",
  comment: "tickets.approval.effect.comment",
};

const START_LABELS: Record<string, TextKey> = {
  "start-now": "tickets.approval.start.start-now",
  "wait-for-run": "tickets.approval.start.wait-for-run",
  "refused-question-pending": "tickets.approval.start.refused-question-pending",
  "recorded-without-runs-control": "tickets.approval.start.recorded-without-runs-control",
  "answers-handback": "tickets.approval.start.answers-handback",
};

export const QUESTION_PENDING_ARM = "refused-question-pending";

export interface ApprovalEffectLabel {
  /** "Approving continues this ticket's series." */
  effect: string;
  /** The start arm's sentence; null when the preview names none. */
  start: string | null;
  /** False while a run question pends (9a59m refuses the start) — no Approve is offered. */
  approvable: boolean;
}

export function approvalEffectLabel(t: T, preview: Pick<TicketApprovalPreview, "effect" | "start">): ApprovalEffectLabel {
  const known = EFFECT_LABELS[preview.effect];
  const effect = t("tickets.approval.approving", { effect: known ? t(known) : preview.effect });
  const arm = preview.start?.kind ?? null;
  return {
    effect,
    start: arm ? (START_LABELS[arm] ? t(START_LABELS[arm]) : arm) : null,
    approvable: arm !== QUESTION_PENDING_ARM,
  };
}

const DISPOSITION_MARKS: Record<string, string> = { kept: " ", recut: "~", appended: "+", dropped: "−" };

/** The mark a draft line carries for its disposition (kept, recut, appended, dropped). */
export function dispositionMark(disposition: string | null | undefined): string {
  return disposition ? DISPOSITION_MARKS[disposition] ?? "?" : "+";
}

/** What the approval card on a ticket-bound conversation says and whether it offers Approve. */
export interface TicketApprovalEffect {
  summary: string;
  approvable: boolean;
}

export function ticketApprovalEffect(t: T, preview: Pick<TicketApprovalPreview, "effect" | "start">): TicketApprovalEffect {
  const label = approvalEffectLabel(t, preview);
  return { summary: label.start ? `${label.effect} ${label.start}` : label.effect, approvable: label.approvable };
}
