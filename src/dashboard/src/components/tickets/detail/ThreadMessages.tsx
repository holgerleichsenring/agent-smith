"use client";

import { useState, type ReactNode, type RefObject } from "react";
import Link from "next/link";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { runPath } from "@/lib/runPath";
import { shortRepo } from "@/lib/shortRepo";
import { useTicketDetail, useTicketSeries, useTicketText } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDerivation } from "@/types/tickets";
import { money, platformWords } from "../ticketFormat";
import { reportStopped, reportText, runLine, specsOf } from "./reportWords";
import { RUNS_CONTROL } from "./RetryUnchanged";
import { RunInlineLog } from "./RunInlineLog";
import { SpecCard } from "./SpecCard";
import { derivationStoryLine, foldStoryLine, runStoryLine, ticketLead } from "./storyLines";
import { StoryRow } from "./StoryRow";
import { derivationWords, type ThreadItem } from "./threadItems";
import { TicketTextSection } from "./TicketTextSection";

// 2026-11-16-6877a: the thread's own messages beside the conversation entries — the ticket as its tracker wrote it,
// each attempt's report (its end in its own words, the spec it delivered, its pull requests, a question it waits on,
// and the line to the run), a spec derivation, and a folded repeat.
// 2026-11-17-8d1cb: each is one line of What happened (StoryRow); its body opens under it. A run's body adds the run's
// sectioned log (RunInlineLog). The newest run and a run that waits on a question open by themselves.

/** The ticket first: its first sentence, the whole ticket as written when opened. */
export function TicketTextMessage() {
  const { data: text } = useTicketText();
  const summary = useTicketDetail().data?.summary;
  const [open, setOpen] = useState(false);
  if (!text || text.source === "unavailable" || !summary) return null;
  const line = { at: text.at, who: summary.platform ? platformWords(summary.platform) : "The ticket", outcome: null,
    text: ticketLead(text.description), meta: "", tone: "person" as const };
  return (
    <StoryRow line={line} open={open} onToggle={() => setOpen(!open)} testId="thread-ticket" className="tk-ticket">
      <TicketTextSection />
    </StoryRow>
  );
}

interface ReportProps {
  attempt: TicketAttempt;
  /** Opened at first: the newest run, or the one whose question waits. */
  initiallyOpen: boolean;
  /** Set on the attempt whose question waits: the card is scrolled to on open. */
  questionRef?: RefObject<HTMLDivElement | null>;
}

export function AttemptReport({ attempt, initiallyOpen, questionRef }: ReportProps) {
  const { project } = useTicketDetail();
  const series = useTicketSeries().data;
  const [open, setOpen] = useState(initiallyOpen || !!attempt.pendingQuestion);
  const specs = specsOf(attempt, series);
  const prs = attempt.pullRequests.filter((p) => p.url);
  const line = runStoryLine(attempt);
  const said = reportText(attempt);
  return (
    <StoryRow line={line} open={open} onToggle={() => setOpen(!open)} testId={`thread-report-${attempt.number}`}
      className={reportStopped(attempt) ? "stopped" : undefined}>
      {said !== line.text && <div className="tk-said" data-testid="thread-report-text">{said}</div>}
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
      <RunInlineLog attempt={attempt} />
      <Link className="tk-runline" href={runPath(attempt.runId)} data-testid={`thread-run-${attempt.number}`}>
        {runLine(attempt)} · open the run ›
      </Link>
    </StoryRow>
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
  const [open, setOpen] = useState(false);
  return (
    <StoryRow line={derivationStoryLine(derivation)} open={open} onToggle={() => setOpen(!open)}
      testId={`spec-derivation-${derivation.runId}`} className={derivation.outcome === "not-finished" ? "stopped" : undefined}>
      <div className="tk-said" data-testid={`spec-derivation-outcome-${derivation.runId}`}>Spec derivation {derivationWords(derivation)}.</div>
      {derivation.summary && <div className="tk-said tk-dim">{derivation.summary}</div>}
      {derivation.pullRequests.map((url) => <a key={url} className="tk-link" href={url} target="_blank" rel="noreferrer">Spec pull request ↗</a>)}
      <Link className="tk-runline" href={runPath(derivation.runId)}>Derivation · {money(derivation.cost)} · open the run ›</Link>
    </StoryRow>
  );
}

/** A folded run of equal repeats: one line ("Runs 3–6 · 4× same cause"), the lines it holds opened on demand. */
export function RepeatLine({ items, testId, children }: { items: ThreadItem[]; testId: string; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <StoryRow line={foldStoryLine(items)} open={open} onToggle={() => setOpen(!open)} testId={testId} className="tk-fold">
      <div className="tk-fold-body">{children}</div>
    </StoryRow>
  );
}
