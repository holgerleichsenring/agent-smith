import type { ReactNode } from "react";
import type { TicketPullRequest, TicketSummary } from "@/types/tickets";
import { age, causeWords, failedAttempts, outcomeWords } from "./ticketFormat";

// 2026-10-13-9a59p: the mock's .act line — what the ticket is doing and why, written only from
// fields the row carries. No step or pass (the spec ledger is per run), no "sandboxes released",
// no author where none is recorded.

const UNTRUSTED = "not a trusted reviewer";

export function TicketActivity({ ticket }: { ticket: TicketSummary }) {
  const parts = partsOf(ticket).filter((p): p is ReactNode => p !== null && p !== false);
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

function partsOf(t: TicketSummary): Array<ReactNode | null | false> {
  const attempt = `attempt ${t.attemptCount}`;
  const failed = failedAttempts(t.attempts);
  switch (t.bucket) {
    case "needs_you":
      return [
        <span key="q" className="aq">{t.kind === "handback" ? `handed back in ${attempt}` : `question in ${attempt}`}</span>,
        specs(t),
        cause(t),
      ];
    case "working":
      return [attempt, t.status === "running" ? null : queued(t), specs(t), cause(t)];
    case "waiting_on_review":
    case "done":
      return pullRequests(t);
    case "stuck":
      return [<span key="f" className="ab">{failed === 1 ? "1 attempt failed" : `${failed} attempts failed`}</span>, checked(t)];
    default:
      return [
        t.lastOutcome ? <>last attempt <b>{outcomeWords(t.lastOutcome)}</b></> : null,
        failed > 1 && `${failed} attempts failed`,
        ...pullRequests(t),
      ];
  }
}

function queued(t: TicketSummary): ReactNode {
  return <b>{t.queuePlace != null ? `queued, place ${t.queuePlace}` : "queued"}</b>;
}

function specs(t: TicketSummary): ReactNode | null {
  return t.specsTotal > 0 ? <b>{`${t.specsDone} of ${t.specsTotal} specs done`}</b> : null;
}

function cause(t: TicketSummary): ReactNode | null {
  if (!t.cause) return null;
  const when = age(t.cause.at);
  return <>started by <b>{causeWords(t.cause)}</b>{when ? ` ${when} ago` : ""}</>;
}

function checked(t: TicketSummary): string | null {
  const when = age(t.checkedAt);
  if (t.unchangedSinceLastAttempt === true) return when ? `unchanged since the last attempt, checked ${when} ago` : "unchanged since the last attempt";
  return t.unchangedSinceLastAttempt == null && when ? `not checked since ${when} ago` : null;
}

function pullRequests(t: TicketSummary): ReactNode[] {
  return (t.pullRequests ?? []).map((pr) => <PullRequestWords key={pr.url} pr={pr} />);
}

function PullRequestWords({ pr }: { pr: TicketPullRequest }) {
  const when = age(pr.lastActivityAt);
  return (
    <>
      PR #{pr.number} {pr.state}
      <Decision pr={pr} />
      {when ? ` · ${when} ago` : ""}
    </>
  );
}

function Decision({ pr }: { pr: TicketPullRequest }) {
  const by = pr.by ? ` by ${pr.by}` : "";
  switch (pr.decision) {
    case "approved":
      return <> · <b>approved{by}</b></>;
    case "changes-requested":
      return <> · <b>changes requested{by}</b></>;
    case "rejected":
      return <> · <b>rejected{by}</b></>;
    case "changes-requested-untrusted":
      return <> · <b>changes requested{by}</b> · {UNTRUSTED}</>;
    default:
      return pr.state === "open" ? <> · no review</> : null;
  }
}
