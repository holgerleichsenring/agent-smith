import type { T } from "@/lib/i18n/translate";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunMergeStep, SeriesRunTicket } from "@/types/seriesRuns";
import { holdRowAct, holdSentence, isPersons, mergeTail, namesOf } from "../../tickets/holds/holdWords";
import { holdPullRequestHref, holdSeriesHref } from "../../tickets/holds/holdRoutes";

// 2026-11-23-849e: the series page's "Your next step", read from the tickets' holds alone — one line per person's hold
// (answer a question, review and merge the next pull request, talk about the plan) with its act, then whether merging is
// what moves the rest: how many tickets go on without a merge and which need merges to start.
// 2026-11-23-8d16: the merge's line carries the merge act itself (merge: the ticket id), not a link to Merge next, and says
// how many merges the waiting tickets need — the steps the server marked (neededBy), never a count of its own.

export interface SeriesStepLine {
  ticketId: string;
  text: string;
  act: { label: string; href: string; external: boolean };
  /** 2026-11-23-8d16: the line is the series' merge — its button runs the merge of this ticket rather than following act. */
  merge?: boolean;
}

/** A step of the merge order is the person's merge now only while its ticket's hold is that merge — CI running or red,
 *  an attempt at work on it, or an escalation is not; the same rule the server's next merge reads. */
export function mergesNow(detail: SeriesRunDetail, step: SeriesRunMergeStep): boolean {
  return step.wait === "ready" && detail.tickets.find((t) => t.ticketId === step.ticketId)?.hold?.case === "merge_next";
}

/** One line per person's hold of the series, in the page's ticket order, each with the act it names. */
export function seriesStepLines(detail: SeriesRunDetail, seriesHref: string, t: T): SeriesStepLine[] {
  return detail.tickets.filter((ticket) => isPersons(ticket.hold)).map((ticket) => line(detail, ticket, seriesHref, t));
}

function line(detail: SeriesRunDetail, ticket: SeriesRunTicket, seriesHref: string, t: T): SeriesStepLine {
  const hold = ticket.hold!;
  if (hold.case === "merge_next") {
    const prs = ticket.pullRequests.filter((pr) => pr.state === "open" || pr.state === "unread")
      .map((pr) => t("series.merge.pr", { number: pr.number }));
    const text = [t("series.you.merge", { prs: prs.length > 0 ? namesOf(prs, t) : ticket.key, key: ticket.key }),
      mergeTail({ ...hold, names: [] }, t), hold.approves ? t("hold.merge.approves") : null].filter(Boolean).join(" ");
    return { ticketId: ticket.ticketId, text, merge: true,
      act: { label: holdRowAct(hold, t), href: `${seriesHref}?section=next`, external: false } };
  }
  const external = holdPullRequestHref(hold);
  const href = holdSeriesHref(hold, () => seriesHref) ?? external ?? ticketPath(detail.project, ticket.ticketId);
  return { ticketId: ticket.ticketId, text: `${ticket.key}: ${holdSentence(hold, t)}`, act: { label: holdRowAct(hold, t), href, external: !!external } };
}

/** "Must I merge?": the open tickets still worked or to be worked without a merge, and the ones that need merges to start;
 *  a ticket whose pull request waits is a merge itself and counts in neither. Null when none is open. */
export function seriesMergeLine(detail: SeriesRunDetail, t: T): string | null {
  const open = detail.tickets.filter((ticket) => !ticket.landed && ticket.hold);
  if (open.length === 0) return null;
  const need = open.filter((ticket) => ticket.hold?.case === "starts_on_merge").map((ticket) => ticket.key);
  const merging = open.filter((ticket) => ticket.pullRequests.some((pr) => pr.state === "open" || pr.state === "unread")).length;
  const without = t("series.you.without", { count: open.length - need.length - merging });
  const marked = detail.mergeOrder.filter((step) => (step.neededBy ?? []).length > 0);
  const waits = [...new Set(marked.flatMap((step) => step.neededBy ?? []))];
  const needs = marked.length > 0
    ? t("series.you.neededFor", { count: waits.length, names: namesOf(waits, t), merges: t("series.you.neededMerges", { count: marked.length }) })
    : need.length > 0 ? t("series.you.need", { count: need.length, names: namesOf(need, t) }) : t("series.you.needNone");
  return t("series.you.merges", { without, need: needs });
}
