"use client";

import Link from "next/link";
import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { mergeSeriesTicket } from "@/lib/seriesRunsApi";
import { refreshTicketBase } from "@/lib/ticketsApi";
import { ProjectInitAction } from "@/components/system/ProjectInitAction";
import { TrackerText } from "@/components/ui/TrackerText";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { runPath, runTracePath } from "@/lib/runPath";
import { shortRepo } from "@/lib/shortRepo";
import type { TicketAttempt, TicketDetail, TicketPullRequest } from "@/types/tickets";
import { prNumber } from "./conversationWords";
import { RetryUnchanged, RUNS_CONTROL } from "./RetryUnchanged";
import type { StateAct } from "./ticketState";
import { useTicketTabGo } from "./useTicketTabGo";
import { useYourMove } from "./useYourMove";
import { actLabel, type YourMove } from "./yourMove";
import { useDateLocale, useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { waitClock } from "@/components/jobs/runlog/modelWaitWords";

// 2026-11-16-6877a: the one box above the composer that says what is next — a coloured dot, one title, ticketState's
// sentence, at most one bright act, and for a stopped ticket the run's own end text behind "Why". It takes the
// place of the state block under the header (2026-10-16-86e6c); the information is the same, the place is new.
// 2026-11-23-31dcg: what the box says and offers is the ticket's hold; a merge hold's act merges through the series.

interface Props {
  detail: TicketDetail;
  /** Focuses the composer: the act of a ticket whose next step is a reply. */
  onReply: () => void;
  /** Brings the waiting question into view. */
  onAnswer: () => void;
}

export function YourMoveBox({ detail, onReply, onAnswer }: Props) {
  const t = useT();
  const locale = useDateLocale();
  const read = useYourMove(detail);
  if (!read) return null;
  const { view } = read;
  // 2026-11-22-50d7a: a working run that waits for its model place says so, with since when, instead of "Working".
  const wait = view.modelWait;
  const move: YourMove = wait && view.attempt ? {
    ...read.move,
    title: t("run.modelWait.title"),
    text: t("run.modelWait.sentence", { n: view.attempt.number, at: waitClock(wait.since, locale) }),
  } : read.move;
  return (
    <section className={`tk-move ${move.tone}`} data-testid="ticket-state" data-tone={move.tone} aria-live="polite">
      <span className="tk-move-dot" aria-hidden="true" />
      <div className="tk-move-say">
        <span className="tk-move-title" data-testid="ticket-move-title">{move.title}</span>
        <span className="tk-move-text" data-testid="ticket-state-sentence">{move.text}</span>
        {move.detail && (
          <div className={move.detailFormat ? "tk-move-detail md" : "tk-move-detail"} data-testid="ticket-state-detail">
            {move.detailFormat ? <TrackerText text={move.detail} format={move.detailFormat} /> : move.detail}
          </div>
        )}
        {view.pullRequests.length > 0 && <PullRequests prs={view.pullRequests} />}
      </div>
      <Act act={move.act} project={detail.summary.project} onReply={onReply} onAnswer={onAnswer} question={move.question} />
      {move.why && <Why move={move} attempt={view.attempt} />}
    </section>
  );
}

interface ActProps {
  act: StateAct | null;
  project: string;
  onReply: () => void;
  onAnswer: () => void;
  /** A run waits on a question: the conversation act brings the question card into view. */
  question: boolean;
}

function Act({ act, project, onReply, onAnswer, question }: ActProps) {
  const address = useTicketTabGo();
  const t = useT();
  if (!act) return null;
  switch (act.kind) {
    case "merge": return <span className="tk-move-act"><MergeAct seriesId={act.seriesId} ticketId={act.ticketId} project={project} /></span>;
    case "retry": return <span className="tk-move-act"><RetryUnchanged attempt={act.attempt} project={project} /></span>;
    case "refresh": return <span className="tk-move-act"><RefreshAct project={act.project} ticketId={act.ticketId} url={act.url} /></span>;
    case "init": return <span className="tk-move-act" data-testid="ticket-state-init"><ProjectInitAction project={act.project} /></span>;
    case "link":
      return (
        <span className="tk-move-act">
          <Link className="btn primary" href={act.href} data-testid="ticket-state-act">{act.label}</Link>
          {act.note && <span className="tk-dim">{act.note}</span>}
        </span>
      );
    case "tab": {
      const go = act.tab === "conversation" ? (question ? onAnswer : onReply) : () => address.go(act.tab);
      return (
        <span className="tk-move-act">
          <button type="button" className={question ? "btn primary" : "btn"} data-testid="ticket-state-act" onClick={go}>
            {actLabel(t, act, question)}
          </button>
        </span>
      );
    }
  }
}

/** 2026-11-23-31dcg: the merge a merge hold offers — the series' merge run (31dce); what it did, or why it refused, stays
 *  under the button. Hidden from a caller known to lack runs.control. */
function MergeAct({ seriesId, ticketId, project }: { seriesId: string; ticketId: string; project: string }) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  if (lacks) return null;
  const merge = async () => {
    setBusy(true);
    setSaid(null);
    try {
      const result = await mergeSeriesTicket(seriesId, ticketId);
      setSaid(result.kind === "said" ? result.text
        : result.answer.pullRequests.map((pr) => (pr.outcome === "merged" ? `${pr.repo}: ${t("series.merge.outcome.merged")}` : pr.text)).join(" · "));
    } catch (error: unknown) {
      setSaid(t("hold.act.mergeFailed", { message: error instanceof Error ? error.message : String(error) }));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <button type="button" className="btn primary" disabled={busy} onClick={() => void merge()} data-testid="ticket-state-act">
        {busy ? t("hold.act.merging") : t("hold.act.merge")}
      </button>
      {said && <span className="tk-dim" role="status" data-testid="ticket-state-merge-said">{said}</span>}
    </>
  );
}

/** 2026-11-24-9dc0e: Try again on a refused refresh — the same refresh-base run for that pull request; what the server
 *  said stays under the button. Without runs.control the act is the pull request itself. */
function RefreshAct({ project, ticketId, url }: { project: string; ticketId: string; url: string }) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [busy, setBusy] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  if (lacks) return <a className="btn primary" href={url} target="_blank" rel="noreferrer" data-testid="ticket-state-act">{t("hold.act.pull_request")}</a>;
  const tryAgain = async () => {
    setBusy(true);
    setSaid(null);
    try {
      const answer = await refreshTicketBase(project, ticketId, url);
      setSaid(answer.kind === "started" ? t("hold.act.refreshStarted") : answer.text);
    } catch (error: unknown) {
      setSaid(t("hold.act.refreshFailed", { message: error instanceof Error ? error.message : String(error) }));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <button type="button" className="btn primary" disabled={busy} onClick={() => void tryAgain()} data-testid="ticket-state-act">
        {busy ? t("hold.act.refreshing") : t("hold.act.refresh")}
      </button>
      {said && <span className="tk-dim" role="status" data-testid="ticket-state-refresh-said">{said}</span>}
    </>
  );
}

function Why({ move, attempt }: { move: YourMove; attempt: TicketAttempt | null }) {
  const t = useT();
  if (!move.why || !attempt) return null;
  return (
    <details className="tk-move-why" data-testid="ticket-state-why">
      <summary>{t("tickets.move.why")}</summary>
      <div className="tk-move-why-body">
        {attempt.summary
          ? <pre className="ts-cause" data-testid="ticket-state-cause">{formatRunSummary(attempt.summary)}</pre>
          : <span className="tk-dim">{t("tickets.move.noEndText")}</span>}
        <span className="ts-links">
          <Link className="btn sm" href={runPath(attempt.runId)} data-testid="ticket-state-run">{t("tickets.move.run", { n: attempt.number })}</Link>
          <Link className="btn sm" href={runTracePath(attempt.runId)} data-testid="ticket-state-trace">{t("tickets.move.trace")}</Link>
        </span>
      </div>
    </details>
  );
}

const PR_STATES: Record<string, TextKey> = {
  open: "tickets.activity.prState.open", merged: "tickets.activity.prState.merged", closed: "tickets.activity.prState.closed",
};
const PR_DECISIONS: Record<string, TextKey> = {
  approved: "tickets.activity.approved", "changes-requested": "tickets.activity.changesRequested", rejected: "tickets.activity.rejected",
};

function PullRequests({ prs }: { prs: TicketPullRequest[] }) {
  const t = useT();
  const word = (keys: Record<string, TextKey>, value: string, raw: string) => (keys[value] ? t(keys[value]) : raw);
  return (
    <ul className="tk-move-prs" data-testid="ticket-state-prs">
      {prs.map((pr) => (
        <li key={pr.url}>
          <a href={pr.url} target="_blank" rel="noreferrer" title={pr.repo} data-testid="ticket-state-pr">
            <span className="mono">{shortRepo(pr.repo)}</span> #{prNumber(pr.url) ?? pr.number}
          </a>
          <span className="tk-dim"> {word(PR_STATES, pr.state, pr.state)}{pr.decision !== "none" ? ` · ${word(PR_DECISIONS, pr.decision, pr.decision.replace(/-/g, " "))}` : ""}</span>
          {pr.target && <span className="tk-dim" data-testid="ticket-state-pr-target"> · {t("tickets.pr.into", { branch: pr.target })}</span>}
        </li>
      ))}
    </ul>
  );
}
