"use client";

import Link from "next/link";
import { ProjectInitAction } from "@/components/system/ProjectInitAction";
import { TrackerText } from "@/components/ui/TrackerText";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { runPath, runTracePath } from "@/lib/runPath";
import { shortRepo } from "@/lib/shortRepo";
import type { TicketAttempt, TicketDetail, TicketPullRequest } from "@/types/tickets";
import { prNumber } from "./conversationWords";
import { RetryUnchanged } from "./RetryUnchanged";
import type { StateAct } from "./ticketState";
import { useTicketTabGo } from "./useTicketTabGo";
import { useYourMove } from "./useYourMove";
import type { YourMove } from "./yourMove";

// 2026-11-16-6877a: the one box above the composer that says what is next — a coloured dot, one title, ticketState's
// sentence, at most one bright act, and for a stopped ticket the run's own end text behind "Why". It takes the
// place of the state block under the header (2026-10-16-86e6c); the information is the same, the place is new.

interface Props {
  detail: TicketDetail;
  /** Focuses the composer: the act of a ticket whose next step is a reply. */
  onReply: () => void;
  /** Brings the waiting question into view. */
  onAnswer: () => void;
}

export function YourMoveBox({ detail, onReply, onAnswer }: Props) {
  const read = useYourMove(detail);
  if (!read) return null;
  const { view, move } = read;
  const review = detail.summary.bucket === "waiting_on_review";
  const openPrs = view.pullRequests.filter((pr) => pr.state === "open");
  const prAct: StateAct | null = review && openPrs.length === 1
    ? { kind: "link", label: "Review the pull request", href: openPrs[0].url } : null;
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
      <Act act={prAct ?? move.act} project={detail.summary.project} onReply={onReply} onAnswer={onAnswer}
        question={detail.summary.bucket === "needs_you" && detail.summary.kind === "question"} />
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
  if (!act) return null;
  switch (act.kind) {
    case "retry": return <span className="tk-move-act"><RetryUnchanged attempt={act.attempt} project={project} /></span>;
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
            {question ? "Answer the question" : "Reply below"}
          </button>
        </span>
      );
    }
  }
}

function Why({ move, attempt }: { move: YourMove; attempt: TicketAttempt | null }) {
  if (!move.why || !attempt) return null;
  return (
    <details className="tk-move-why" data-testid="ticket-state-why">
      <summary>Why</summary>
      <div className="tk-move-why-body">
        {attempt.summary
          ? <pre className="ts-cause" data-testid="ticket-state-cause">{formatRunSummary(attempt.summary)}</pre>
          : <span className="tk-dim">The run recorded no end text.</span>}
        <span className="ts-links">
          <Link className="btn sm" href={runPath(attempt.runId)} data-testid="ticket-state-run">Run {attempt.number}</Link>
          <Link className="btn sm" href={runTracePath(attempt.runId)} data-testid="ticket-state-trace">Trace</Link>
        </span>
      </div>
    </details>
  );
}

function PullRequests({ prs }: { prs: TicketPullRequest[] }) {
  return (
    <ul className="tk-move-prs" data-testid="ticket-state-prs">
      {prs.map((pr) => (
        <li key={pr.url}>
          <a href={pr.url} target="_blank" rel="noreferrer" title={pr.repo} data-testid="ticket-state-pr">
            <span className="mono">{shortRepo(pr.repo)}</span> #{prNumber(pr.url) ?? pr.number}
          </a>
          <span className="tk-dim"> {pr.state}{pr.decision !== "none" ? ` · ${pr.decision.replace(/-/g, " ")}` : ""}</span>
        </li>
      ))}
    </ul>
  );
}
