import type { ReactNode } from "react";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";
import { TICKET_CLOSED_TRACKER, type TicketPullRequest, type TicketSummary } from "@/types/tickets";
import { age, ago, causeWords, failedAttempts, outcomeWords } from "./ticketFormat";
import { holdAsk, holdSentence, holdTag } from "./holds/holdWords";

// 2026-10-13-9a59p: the mock's .act line — what the ticket is doing and why, written only from
// fields the row carries. No step or pass (the spec ledger is per run), no "sandboxes released",
// no author where none is recorded.

/** The statuses of an attempt still at work — running, queued, or relaunching. */
const LIVE = new Set(["running", "queued", "waiting_for_input"]);

export function TicketActivity({ ticket }: { ticket: TicketSummary }) {
  const t = useT();
  const parts = partsOf(t, ticket).filter((p): p is ReactNode => p !== null && p !== false);
  if (parts.length === 0) return null;
  return (
    <div className="act" data-testid="ticket-activity">
      {parts.map((part, i) => (
        <span key={i}>
          {i > 0 && " · "}
          {part}
        </span>
      ))}
    </div>
  );
}

function partsOf(tr: T, t: TicketSummary): Array<ReactNode | null | false> {
  const attempt = tr("tickets.activity.attempt", { n: t.attemptCount });
  const failed = failedAttempts(t.attempts);
  switch (t.bucket) {
    case "needs_you":
      // 2026-11-23-31dcg: whose move it is, by the hold's tag; a question or hand-back names the attempt it came from.
      return [
        <span key="q" className="aq">
          {t.hold?.case === "handback" ? tr("tickets.activity.handedBackIn", { attempt }) : t.hold?.case === "question" ? tr("tickets.activity.questionIn", { attempt })
            : t.hold ? holdTag(t.hold, tr) : null}
        </span>,
        // 2026-11-23-849e: the hold's words sit in the row now that no block under it carries them — the question, the
        // hand-back or the sentence (a series' merge says what it unblocks).
        t.hold ? <span key="s">{holdAsk(t, tr)}</span> : null,
        specs(tr, t),
        cause(tr, t),
        // 2026-11-23-d60d: a pull request to merge, or one whose CI failed, keeps its review decision in sight.
        ...(t.hold?.act?.kind === "pull_request" ? pullRequests(tr, t) : []),
      ];
    case "working":
      // 2026-11-23-d60d: an ended attempt whose pull request waits says what it waits for, beside its pull requests.
      if (!LIVE.has(t.status)) return [t.hold ? <span key="w">{holdSentence(t.hold, tr)}</span> : null, ...pullRequests(tr, t)];
      return [attempt, t.status === "running" ? null : queued(tr, t), specs(tr, t), cause(tr, t)];
    case "done":
      return pullRequests(tr, t);
    case "blocked":
      // 2026-11-23-849e: a ticket that starts once others merge says how many merges it waits for and which comes next.
      if (t.hold?.case === "starts_on_merge") return [<span key="m">{holdSentence(t.hold, tr)}</span>];
      return [filedWords(tr, t.since)];
    case "filed":
      return [filedWords(tr, t.since)];
    case "closed":
      return [closedWords(tr, t)];
    case "stuck":
      // 2026-11-23-31dci: a wait reported to the admin failed nothing — the row says what it waits for and that the admin knows.
      if (t.hold?.escalation) return [<span key="e" className="aq">{holdSentence(t.hold, tr)}</span>];
      return [<span key="f" className="ab">{tr("tickets.activity.attemptsFailed", { count: failed })}</span>, checked(tr, t), ...pullRequests(tr, t)];
    default:
      return [
        t.lastOutcome ? <>{tr("tickets.activity.lastAttempt")} <b>{outcomeWords(tr, t.lastOutcome)}</b></> : null,
        failed > 1 && tr("tickets.activity.attemptsFailed", { count: failed }),
        ...pullRequests(tr, t),
      ];
  }
}

/** 2026-10-14-dbcen: when a never-run ticket was filed — "filed 12m ago". */
export function filedWords(t: T, since: string | null): string {
  const when = age(since, t);
  return when === null ? t("tickets.activity.filed") : when === "now" ? t("tickets.activity.filedJustNow")
    : t("tickets.activity.filedAgo", { when });
}

/** 2026-10-15-114fd: why a closed ticket is closed — "closed by Ada 2h ago: superseded", or closed on the tracker. */
export function closedWords(tr: T, t: TicketSummary): string {
  if (t.kind === TICKET_CLOSED_TRACKER) return tr("tickets.activity.closedOnTracker");
  if (!t.closed) return tr("tickets.activity.closed");
  const when = age(t.closed.at, tr);
  const by = t.closed.by;
  const head = when === null ? tr("tickets.activity.closedBy", { by }) : when === "now" ? tr("tickets.activity.closedByJustNow", { by })
    : tr("tickets.activity.closedByAgo", { by, when });
  return `${head}${t.closed.note ? `: ${t.closed.note}` : ""}`;
}

function queued(tr: T, t: TicketSummary): ReactNode {
  return <b>{t.queuePlace != null ? tr("tickets.activity.queuedPlace", { place: t.queuePlace }) : tr("tickets.activity.queued")}</b>;
}

function specs(tr: T, t: TicketSummary): ReactNode | null {
  return t.specsTotal > 0 ? <b>{tr("tickets.activity.specsDone", { done: t.specsDone, total: t.specsTotal })}</b> : null;
}

/** 2026-11-22-50d7a: "restarted by Ada 2h ago" — the person by name, "someone" where none is recorded. */
function cause(tr: T, t: TicketSummary): ReactNode | null {
  if (!t.cause) return null;
  const since = ago(tr, t.cause.at);
  return <><b>{causeWords(tr, t.cause)}</b>{since ? ` ${since}` : ""}</>;
}

function checked(tr: T, t: TicketSummary): string | null {
  const when = age(t.checkedAt, tr);
  if (t.unchangedSinceLastAttempt === true) {
    return when ? tr("tickets.activity.unchangedChecked", { when }) : tr("tickets.activity.unchanged");
  }
  return t.unchangedSinceLastAttempt == null && when ? tr("tickets.activity.notChecked", { when }) : null;
}

function pullRequests(tr: T, t: TicketSummary): ReactNode[] {
  return (t.pullRequests ?? []).map((pr) => <PullRequestWords key={pr.url} tr={tr} pr={pr} />);
}

const PR_STATE_KEYS: Record<string, TextKey> = {
  open: "tickets.activity.prState.open", merged: "tickets.activity.prState.merged", closed: "tickets.activity.prState.closed",
};

function PullRequestWords({ tr, pr }: { tr: T; pr: TicketPullRequest }) {
  const when = age(pr.lastActivityAt, tr);
  const state = PR_STATE_KEYS[pr.state] ? tr(PR_STATE_KEYS[pr.state]) : pr.state;
  return (
    <>
      {tr("tickets.activity.pr", { number: pr.number, state })}
      <Decision tr={tr} pr={pr} />
      {when ? ` · ${tr("tickets.start.ago", { when })}` : ""}
    </>
  );
}

function Decision({ tr, pr }: { tr: T; pr: TicketPullRequest }) {
  const by = (key: TextKey) => (pr.by ? tr(`${key}By` as TextKey, { by: pr.by }) : tr(key));
  switch (pr.decision) {
    case "approved":
      return <> · <b>{by("tickets.activity.approved")}</b></>;
    case "changes-requested":
      return <> · <b>{by("tickets.activity.changesRequested")}</b></>;
    case "rejected":
      return <> · <b>{by("tickets.activity.rejected")}</b></>;
    case "changes-requested-untrusted":
      return <> · <b>{by("tickets.activity.changesRequested")}</b> · {tr("tickets.activity.untrusted")}</>;
    default:
      return pr.state === "open" ? <> · {tr("tickets.activity.noReview")}</> : null;
  }
}
