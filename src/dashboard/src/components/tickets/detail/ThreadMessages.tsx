"use client";

import { useState, type ReactNode, type RefObject } from "react";
import Link from "next/link";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { TrackerText } from "@/components/ui/TrackerText";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { runPath } from "@/lib/runPath";
import { shortRepo } from "@/lib/shortRepo";
import { useTicketDetail, useTicketSeries, useTicketText } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDerivation } from "@/types/tickets";
import { money, platformWords } from "../ticketFormat";
import { clock } from "./flowWords";
import { reportStopped, reportText, runLine, specsOf } from "./reportWords";
import { RUNS_CONTROL } from "./RetryUnchanged";
import { SpecCard } from "./SpecCard";
import { derivationWords } from "./threadItems";

// 2026-11-16-6877a: the thread's own messages beside the conversation entries — the ticket as its tracker wrote it,
// each attempt's report (its end in its own words, the spec it delivered, its pull requests, a question it waits on,
// and the line to the run), a spec derivation, and a folded repeat.

function AgentMessage({ at, children, testId, tone }: { at: string | null; children: ReactNode; testId: string; tone?: string }) {
  return (
    <div className={tone ? `msg tk-msg ${tone}` : "msg tk-msg"} data-testid={testId}>
      <span className="av agent" aria-hidden="true">as</span>
      <div>
        <div className="mh"><b>Agent Smith</b><span className="w">{clock(at)}</span></div>
        {children}
      </div>
    </div>
  );
}

/** The ticket first, cut short until the reader asks for it as written. */
export function TicketTextMessage() {
  const { data: text } = useTicketText();
  const summary = useTicketDetail().data?.summary;
  const [open, setOpen] = useState(false);
  if (!text || text.source === "unavailable" || !summary) return null;
  const tracker = platformWords(summary.platform);
  const long = (text.description?.length ?? 0) > 280 || !!text.acceptanceCriteria;
  return (
    <div className="msg tk-msg" data-testid="thread-ticket">
      <span className="av rv" aria-hidden="true">{tracker.charAt(0).toUpperCase()}</span>
      <div>
        <div className="mh"><b>{summary.platform ? tracker : "The ticket"}</b><span className="w">{clock(text.at)}</span></div>
        <div className={open ? "mb" : "mb tk-short"} data-testid="thread-ticket-text">
          {text.description ? <TrackerText text={text.description} format={text.descriptionFormat} /> : <span className="tk-dim">No description.</span>}
          {open && text.acceptanceCriteria && (
            <div className="tk-ac"><span className="tk-dim">Acceptance criteria</span><TrackerText text={text.acceptanceCriteria} format={text.criteriaFormat} /></div>
          )}
        </div>
        {long && (
          <button type="button" className="tk-link" aria-expanded={open} onClick={() => setOpen(!open)} data-testid="thread-ticket-toggle">
            {open ? "Show less" : "Show the ticket as written"}
          </button>
        )}
      </div>
    </div>
  );
}

interface ReportProps {
  attempt: TicketAttempt;
  /** Set on the attempt whose question waits: the card is scrolled to on open. */
  questionRef?: RefObject<HTMLDivElement | null>;
}

export function AttemptReport({ attempt, questionRef }: ReportProps) {
  const { project } = useTicketDetail();
  const series = useTicketSeries().data;
  const specs = specsOf(attempt, series);
  const prs = attempt.pullRequests.filter((p) => p.url);
  return (
    <AgentMessage at={attempt.finishedAt ?? attempt.startedAt} testId={`thread-report-${attempt.number}`}
      tone={reportStopped(attempt) ? "stopped" : undefined}>
      <div className="mb tk-said" data-testid="thread-report-text">{reportText(attempt)}</div>
      {specs.map((spec) => <SpecCard key={spec.specId} spec={spec} runId={attempt.runId} />)}
      {prs.length > 0 && (
        <div className="tk-prs">
          {prs.map((p) => (
            <a key={p.url} href={p.url!} target="_blank" rel="noreferrer" title={p.repo}>
              Pull request in {shortRepo(p.repo)} ↗
            </a>
          ))}
        </div>
      )}
      {attempt.pendingQuestion && <div ref={questionRef}><PendingQuestion attempt={attempt} project={project} /></div>}
      <Link className="tk-runline" href={runPath(attempt.runId)} data-testid={`thread-run-${attempt.number}`}>
        {runLine(attempt)} · see what it did
      </Link>
    </AgentMessage>
  );
}

function PendingQuestion({ attempt, project }: { attempt: TicketAttempt; project: string }) {
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [answered, setAnswered] = useState(false);
  const question = attempt.pendingQuestion;
  if (!question) return null;
  if (lacks) {
    return (
      <div className="q-item q-head" data-testid="conv-question-text">
        <div className="qt">{question.text}</div>
        <div className="qm">Run {attempt.number} waits for this answer; answering needs runs.control.</div>
      </div>
    );
  }
  return (
    <div className="q-head" data-testid="conv-question">
      <PendingQuestionCard runId={attempt.runId} question={question} answered={answered}
        onAnswered={() => setAnswered(true)} label={`Send and resume run ${attempt.number}`} />
    </div>
  );
}

export function DerivationMessage({ derivation }: { derivation: TicketDerivation }) {
  const cost = derivation.outcome === "running" ? `${money(derivation.cost)} so far` : money(derivation.cost);
  return (
    <AgentMessage at={derivation.finishedAt ?? derivation.startedAt} testId={`spec-derivation-${derivation.runId}`}
      tone={derivation.outcome === "not-finished" ? "stopped" : undefined}>
      <div className="mb tk-said" data-testid={`spec-derivation-outcome-${derivation.runId}`}>Spec derivation {derivationWords(derivation)}.</div>
      {derivation.pullRequests.map((url) => <a key={url} className="tk-link" href={url} target="_blank" rel="noreferrer">Spec pull request ↗</a>)}
      <Link className="tk-runline" href={runPath(derivation.runId)}>Derivation · {cost} · see what it did</Link>
    </AgentMessage>
  );
}

/** A folded run of equal messages: one line with the count, opened on demand. */
export function RepeatLine({ line, testId, children }: { line: string; testId: string; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="tk-repeat" data-testid={testId}>
      <button type="button" className="tk-repeat-head" aria-expanded={open} onClick={() => setOpen(!open)}>
        <span className="tk-chev" aria-hidden="true">{open ? "▾" : "▸"}</span>
        {line}
      </button>
      {open && <div className="tk-repeat-body">{children}</div>}
    </div>
  );
}
