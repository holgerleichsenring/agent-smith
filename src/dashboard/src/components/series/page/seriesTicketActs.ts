import type { T } from "@/lib/i18n/translate";
import { runPath, runTracePath } from "@/lib/runPath";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { holdPullRequestHref, holdSeriesHref } from "../../tickets/holds/holdRoutes";
import { holdRowAct, holdSentence, namesOf } from "../../tickets/holds/holdWords";
import { mergesWhenGreen, seriesMergeSaid } from "./seriesCiState";
import { waitingPullRequests, type SeriesNodeKind } from "./seriesNodeKind";

// 2026-11-23-f562: what the Overview says about one ticket and what can be done with it — the sentence why it has its
// colour (the hold's own sentence, which every surface says) and its acts by colour. Every act is a real one: the series'
// merge (useSeriesMerge), the ticket page's Retry (useSeriesRetry), a link to a route or a pull request, or the chain.

export type SeriesActKind = "merge" | "retry" | "refresh" | "chain" | "link" | "external";

export interface SeriesAct {
  id: string;
  kind: SeriesActKind;
  label: string;
  /** Where a link leads. */
  href?: string;
  /** primary: the accent fill; ask: the person fill; bad: the failure fill. */
  tone?: "primary" | "ask" | "bad";
  /** A merge that is not the one now carries why. */
  disabledBy?: string;
  /** Left out of the action list's row (the node menu shows it). */
  menuOnly?: boolean;
  /** The run a Retry starts again. */
  run?: string;
  /** 2026-11-24-9dc0e: the pull request whose refused refresh Try again starts again. */
  url?: string;
}

// The newest attempt's ends the ticket page's Retry offers (RetryUnchanged.retriable): finished and not a success.
const RETRIES = new Set(["failed", "shortfall", "cancelled"]);

/** Why the ticket has its colour, in one or two sentences. */
export function seriesNodeSentence(ticket: SeriesRunTicket, detail: SeriesRunDetail, kind: SeriesNodeKind, t: T): string {
  if (kind === "merged") return t("series.node.merged");
  const said = ticket.hold ? holdSentence(t, ticket.hold) : t("series.node.notStarted");
  const draft = kind === "now" && waitingPullRequests(ticket.pullRequests).some((pr) => pr.isDraft) ? t("series.merge.draftFirst") : null;
  const needed = detail.mergeOrder.find((step) => step.ticketId === ticket.ticketId)?.neededBy ?? [];
  const lead = kind === "needed" ? t("series.merge.needed", { names: namesOf(t, needed), count: needed.length }) : null;
  // 2026-11-23-0219: what the last merge run said when it did not merge, and why Merge when green cannot arm here.
  const told = seriesMergeSaid(ticket);
  // 2026-11-24-f9c5f: a merge run that started (or could not start) a series pull request's CI said so — said as it is.
  const mergeSaid = told?.status === "completion_refused" && /^(CI started|CI not started|no CI):/.test(told.reason ?? "") ? told.reason
    : told?.status === "completion_refused" ? t("series.merge.said.refused", { reason: told.reason ?? "" })
    : told?.status === "completion_armed" ? t("series.merge.said.armed") : null;
  const arms = mergesWhenGreen(detail, ticket) && !detail.mergeArms ? t(WhenGreenNeeds(detail)) : null;
  return [lead, said, draft, mergeSaid, arms].filter(Boolean).join(" ");
}

// 2026-11-23-0219: a ready step whose CI runs or has not started merges once green — the run re-queues a build that has not
// started and arms the host's auto-complete, which needs respect_branch_protection; without it the act names the key.
function whenGreenAct(detail: SeriesRunDetail, t: T): SeriesAct {
  return { id: "merge", kind: "merge", label: t("series.merge.whenGreen"), tone: "primary",
    disabledBy: detail.mergeArms ? undefined : t(WhenGreenNeeds(detail)) };
}

// 2026-11-24-f9c5f: a merge into a series branch never arms; it merges by itself once its CI is green.
const WhenGreenNeeds = (detail: SeriesRunDetail) => detail.integrationBranch ? "series.merge.whenGreenSeries" : "series.merge.whenGreenNeeds";

/** The ticket's acts by its colour, in the order they are drawn. lacks: the viewer has no runs.control (no Merge, no Retry). */
export function seriesTicketActs(ticket: SeriesRunTicket, detail: SeriesRunDetail, kind: SeriesNodeKind, lacks: boolean, t: T): SeriesAct[] {
  const talk = `${seriesRunPath(detail.id)}?section=talk`;
  const prs = (kind === "merged" ? ticket.pullRequests.filter((pr) => pr.state === "merged") : waitingPullRequests(ticket.pullRequests))
    .map((pr): SeriesAct => ({ id: `pr-${pr.repo}`, kind: "external", href: pr.url, label: t("series.merge.openPr", { number: pr.number }) }));
  const conversation: SeriesAct = { id: "conversation", kind: "link", href: talk, label: t("series.act.conversation") };
  const run = ticket.runId ?? null;
  const acts: SeriesAct[] = [];
  // 2026-11-25-9992a: an escalated CI wait keeps its kind (pr or needed) on the server, so only those merge when green
  const whenGreen = (kind === "pr" || kind === "needed") && mergesWhenGreen(detail, ticket);
  if (whenGreen && !lacks) acts.push(whenGreenAct(detail, t));
  else if ((kind === "now" || kind === "needed" || kind === "pr") && !lacks)
    acts.push({ id: "merge", kind: "merge", label: t("series.merge.button"), tone: kind === "now" ? "primary" : undefined,
      disabledBy: kind === "now" ? undefined : seriesNodeSentence(ticket, detail, kind, t) });
  if (kind === "question") {
    const hold = ticket.hold!;
    const href = holdSeriesHref(hold, seriesRunPath) ?? holdPullRequestHref(hold) ?? ticketPath(detail.project, ticket.ticketId);
    acts.push({ id: "answer", kind: href.startsWith("http") ? "external" : "link", href, label: holdRowAct(t, hold), tone: "ask" });
    if (href !== talk) acts.push(conversation);
  }
  if (kind === "failed") acts.push(...failedActs(ticket, detail, lacks, conversation, t));
  // 2026-11-24-6811d: a ticket being brought up to date links the refresh run, not its finished attempt.
  const refreshRun = ticket.hold?.case === "refreshing" ? ticket.pullRequests.find((pr) => pr.refreshRunId)?.refreshRunId ?? null : null;
  if (kind === "running" && (refreshRun ?? run)) acts.push({ id: "run", kind: "link", href: runPath((refreshRun ?? run)!), label: t("series.act.run") });
  if (kind === "waiting") acts.push({ ...conversation, menuOnly: true });
  // 2026-11-24-6811a: a conflict's act is the pull request itself, so it stays on the row.
  const prOnRow = ticket.hold?.case === "pr_conflict" || ticket.hold?.case === "refresh_refused";
  acts.push(...prs.map((pr) => (!prOnRow && (kind === "failed" || kind === "question" || kind === "merged") ? { ...pr, menuOnly: true } : pr)));
  if (!(kind === "failed" && ticket.hold?.addressee === "admin")) // an admin's act already opens the ticket
    acts.push({ id: "ticket", kind: "link", href: ticketPath(detail.project, ticket.ticketId), label: t("hold.act.open"), menuOnly: true });
  acts.push({ id: "chain", kind: "chain", label: t("series.act.chain"), menuOnly: true });
  return acts;
}

// A failure's acts. An admin's hold leads with its own act, which lives on the ticket page (settings, init), and is never
// retried; any other is retried with the ticket page's Retry — the hold's retry act, or the ticket page's rule (a finished
// attempt that is neither a success nor handed back) — then See why, then its conversation: the ticket's own when the
// hold's conversation names no series, else the series'.
function failedActs(ticket: SeriesRunTicket, detail: SeriesRunDetail, lacks: boolean, series: SeriesAct, t: T): SeriesAct[] {
  const hold = ticket.hold;
  const page = ticketPath(detail.project, ticket.ticketId);
  if (hold?.addressee === "admin") return [{ id: "act", kind: "link", href: page, label: holdRowAct(t, hold), tone: "bad" }];
  const acts: SeriesAct[] = [];
  // 2026-11-24-9dc0e: a refused refresh is tried again on its pull request — the hold's act names it.
  if (hold?.case === "refresh_refused" && hold.act?.target && !lacks)
    acts.push({ id: "refresh", kind: "refresh", label: t("hold.act.refresh"), tone: "bad", url: hold.act.target });
  const run = hold?.act?.kind === "retry" && hold.act.target ? hold.act.target : ticket.runId ?? null;
  const retriable = hold?.act?.kind === "retry" || (RETRIES.has(ticket.status ?? "") && hold?.case !== "handback");
  if (run && retriable && !lacks) acts.push({ id: "retry", kind: "retry", label: t("hold.act.retry"), tone: "bad", run });
  const log = ticket.pullRequests.find((pr) => pr.ci === "red" && pr.ciLog)?.ciLog;
  if (hold?.case === "ci_failed" && log) acts.push({ id: "why", kind: "external", href: log, label: t("series.act.why") });
  else if (ticket.runId && ticket.status !== "success") acts.push({ id: "why", kind: "link", href: runTracePath(ticket.runId), label: t("series.act.why") });
  const own = hold?.act?.kind === "conversation" && !hold.act.target;
  acts.push(own ? { ...series, href: page } : series);
  return acts;
}
