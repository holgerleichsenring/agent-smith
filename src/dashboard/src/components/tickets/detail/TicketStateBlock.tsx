"use client";

import Link from "next/link";
import { ProjectInitAction } from "@/components/system/ProjectInitAction";
import { formatRunSummary } from "@/lib/formatRunSummary";
import { runPath, runTracePath } from "@/lib/runPath";
import { shortRepo } from "@/lib/shortRepo";
import { useLiveAttempt, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDetail, TicketPullRequest } from "@/types/tickets";
import { attemptChips } from "./attemptChips";
import { prNumber } from "./conversationWords";
import { RetryUnchanged } from "./RetryUnchanged";
import { ticketState, type StateAct, type TicketStateView } from "./ticketState";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-16-86e6c: the state block under the header, on every tab. Visible by default: what is going on (pill and
// one sentence), the way to the run it is about, and the one act that fits. One click away: other options, and Why —
// the run's own end text verbatim with its run and trace. A healthy state is one quiet line.
// 2026-10-16-7ed3b: no other options; the pull requests are plain rows and every attempt is one chip opening its run.

export function TicketStateBlock({ detail }: { detail: TicketDetail }) {
  const live = useLiveAttempt();
  const series = useTicketSeries().data;
  const view = ticketState({ detail, liveEvents: live.events, series });
  if (!view) return null;
  return (
    <section className={`tstate ${view.tone}`} data-testid="ticket-state" data-tone={view.tone} aria-live="polite">
      <div className="ts-top">
        <span className={`pill ${view.tone}`} data-testid="ticket-state-pill">{view.pill}</span>
        <div className="ts-say">
          <p className="ts-sentence" data-testid="ticket-state-sentence">{view.sentence}</p>
          {view.detail && <div className="ts-detail" data-testid="ticket-state-detail">{view.detail}</div>}
        </div>
        {view.attempt && (
          <Link className="btn sm" href={runPath(view.attempt.runId)} data-testid="ticket-state-run">
            Open run A{view.attempt.number} ↗
          </Link>
        )}
      </div>
      {view.act && <div className="ts-acts" data-testid="ticket-state-acts"><Act act={view.act} project={detail.summary.project} /></div>}
      {view.pullRequests.length > 0 && <PullRequests prs={view.pullRequests} />}
      <Chips attempts={detail.attempts} />
      {!view.quiet && view.tone === "bad" && <Why view={view} />}
    </section>
  );
}

function Act({ act, project }: { act: StateAct; project: string }) {
  const address = useTicketTabGo();
  const cls = "btn primary";
  switch (act.kind) {
    case "retry": return <RetryUnchanged attempt={act.attempt} project={project} />;
    case "init": return <span className="ts-act" data-testid="ticket-state-init"><ProjectInitAction project={act.project} /></span>;
    case "link":
      return (
        <span className="ts-act">
          <Link className={cls} href={act.href} data-testid="ticket-state-act">{act.label}</Link>
          {act.note && <span className="cp-note">{act.note}</span>}
        </span>
      );
    case "tab":
      return (
        <span className="ts-act">
          <button type="button" className={cls} data-testid="ticket-state-act"
            onClick={() => address.go(act.tab, { [CAUSE_PARAM]: null })}>{act.label}</button>
          {act.note && <span className="cp-note">{act.note}</span>}
        </span>
      );
  }
}

function Why({ view }: { view: TicketStateView }) {
  const attempt = view.attempt;
  if (!attempt) return null;
  return (
    <details className="ts-why" data-testid="ticket-state-why">
      <summary>Why</summary>
      <div className="ts-why-body">
        {attempt.summary
          ? <pre className="ts-cause" data-testid="ticket-state-cause">{formatRunSummary(attempt.summary)}</pre>
          : <span className="cp-note">The run recorded no end text.</span>}
        <span className="ts-links">
          <Link className="btn sm" href={runPath(attempt.runId)}>Run A{attempt.number}</Link>
          <Link className="btn sm" href={runTracePath(attempt.runId)} data-testid="ticket-state-trace">Trace</Link>
        </span>
      </div>
    </details>
  );
}

function PullRequests({ prs }: { prs: TicketPullRequest[] }) {
  return (
    <ul className="ts-prs" data-testid="ticket-state-prs">
      {prs.map((pr) => (
        <li key={pr.url}>
          <a href={pr.url} target="_blank" rel="noreferrer" title={pr.repo} data-testid="ticket-state-pr">
            <span className="mono">{shortRepo(pr.repo)}</span> #{prNumber(pr.url) ?? pr.number}
          </a>
          <span className="ts-pr-state">{pr.state}{pr.decision !== "none" ? ` · ${pr.decision.replace(/-/g, " ")}` : ""}</span>
        </li>
      ))}
    </ul>
  );
}

function Chips({ attempts }: { attempts: TicketAttempt[] }) {
  const chips = attemptChips(attempts);
  if (chips.length === 0) return null;
  return (
    <nav className="ts-chips" aria-label="Attempts" data-testid="ticket-state-chips">
      {chips.map((chip) => (
        <Link key={chip.attempt.runId} href={chip.href} title={chip.title} className={`ts-chip ${chip.tone}${chip.head ? " head" : ""}`}
          aria-current={chip.head ? "true" : undefined} data-testid={`ticket-state-chip-${chip.attempt.number}`}>
          A{chip.attempt.number}
        </Link>
      ))}
    </nav>
  );
}
