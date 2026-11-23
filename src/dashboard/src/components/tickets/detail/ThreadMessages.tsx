"use client";

import { useState, type ReactNode, type RefObject } from "react";
import Link from "next/link";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { runPath } from "@/lib/runPath";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import { holdSeriesHref, isAsked } from "../holds/holdRoutes";
import { shortRepo } from "@/lib/shortRepo";
import { useTicketDetail, useTicketSeries, useTicketText } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDerivation } from "@/types/tickets";
import { money, platformWords } from "../ticketFormat";
import { reportStopped, runLine, specsOf } from "./reportWords";
import { retriable, RetryUnchanged, RUNS_CONTROL } from "./RetryUnchanged";
import { RunInlineLog } from "./RunInlineLog";
import { SpecCard } from "./SpecCard";
import { derivationStoryLine, foldStoryLine, runStoryLine, ticketLead } from "./storyLines";
import { StoryRow } from "./StoryRow";
import { derivationWords, type ThreadItem } from "./threadItems";
import { TicketTextSection } from "./TicketTextSection";
import { attemptStatus } from "@/components/jobs/runlog/runStatusLine";
import { useAttemptStatus, useStatusContext } from "./useAttemptStatus";

// 2026-11-16-6877a: the thread's own messages beside the conversation entries — the ticket as its tracker wrote it,
// each attempt's report (its end in its own words, the spec it delivered, its pull requests, a question it waits on,
// and the line to the run), a spec derivation, and a folded repeat.
// 2026-11-17-8d1cb: each is one line of What happened (StoryRow); its body opens under it. A run's body adds the run's
// sectioned log (RunInlineLog). The newest run and a run that waits on a question open by themselves.
// 2026-11-22-4321a: the body is AttemptBody, which the Runs section opens under a run's line too.

/** The ticket first: its first sentence, the whole ticket as written when opened. */
export function TicketTextMessage() {
  const t = useT();
  const { data: text } = useTicketText();
  const summary = useTicketDetail().data?.summary;
  const [open, setOpen] = useState(false);
  if (!text || text.source === "unavailable" || !summary) return null;
  const line = { at: text.at, who: summary.platform ? platformWords(t, summary.platform) : t("tickets.thread.theTicket"), outcome: null,
    text: ticketLead(t, text.description), meta: "", tone: "person" as const };
  return (
    <StoryRow line={line} open={open} onToggle={() => setOpen(!open)} testId="thread-ticket" className="tk-ticket">
      <TicketTextSection />
    </StoryRow>
  );
}

interface ReportProps {
  attempt: TicketAttempt;
  /** Opened at first: the current run, or the one whose question waits. */
  initiallyOpen: boolean;
  /** 2026-11-18-d6d2d: this report offers Retry unchanged on its attempt (the ticket's newest). */
  retry?: boolean;
  /** Set on the attempt whose question waits: the card is scrolled to on open. */
  questionRef?: RefObject<HTMLDivElement | null>;
  /** 2026-11-22-4321a: the run's sectioned log in the body; the Conversation section draws reports without it. */
  log?: boolean;
}

export function AttemptReport({ attempt, initiallyOpen, retry, questionRef, log = true }: ReportProps) {
  const t = useT();
  const [open, setOpen] = useState(initiallyOpen || !!attempt.pendingQuestion);
  // 2026-11-22-7103a: the row is the run's status line — what happened, then the next step.
  const line = runStoryLine(t, attempt, useAttemptStatus(attempt));
  return (
    <StoryRow line={line} open={open} onToggle={() => setOpen(!open)} testId={`thread-report-${attempt.number}`}
      className={reportStopped(attempt) ? "stopped" : undefined}>
      <AttemptBody attempt={attempt} retry={retry} questionRef={questionRef} log={log} />
    </StoryRow>
  );
}

interface BodyProps {
  attempt: TicketAttempt;
  retry?: boolean;
  questionRef?: RefObject<HTMLDivElement | null>;
  log: boolean;
}

/** 2026-11-22-4321a: a report's body — under its thread line, or under a Runs line. 2026-11-22-7103a: the line above it
 *  is the status line, so the body no longer repeats the run's end text (Your move's Why and the run page keep it). */
export function AttemptBody({ attempt, retry, questionRef, log }: BodyProps) {
  const t = useT();
  const { project } = useTicketDetail();
  const series = useTicketSeries().data;
  const specs = specsOf(attempt, series);
  const prs = attempt.pullRequests.filter((p) => p.url);
  return (
    <>
      {specs.map((spec) => <SpecCard key={spec.specId} spec={spec} attempt={attempt} />)}
      {prs.length > 0 && (
        <div className="tk-prs">
          {prs.map((p) => (
            <a key={p.url} href={p.url!} target="_blank" rel="noreferrer" title={p.repo}>
              {t("tickets.thread.pullRequestIn", { repo: shortRepo(p.repo) })}
            </a>
          ))}
        </div>
      )}
      {attempt.pendingQuestion && <div ref={questionRef}><PendingQuestion attempt={attempt} project={project} /></div>}
      {log && <RunInlineLog attempt={attempt} />}
      {retry && retriable(attempt) && <div className="cp-actions" data-testid={`thread-retry-${attempt.number}`}><RetryUnchanged attempt={attempt} project={project} /></div>}
      <Link className="tk-runline" href={runPath(attempt.runId)} data-testid={`thread-run-${attempt.number}`}>
        {runLine(t, attempt)} · {t("tickets.thread.openRun")}
      </Link>
    </>
  );
}

function PendingQuestion({ attempt, project }: { attempt: TicketAttempt; project: string }) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [answered, setAnswered] = useState(false);
  // 2026-11-23-31dch: a park that asks about the plan is answered in its series' conversation, never here.
  const hold = useTicketDetail().data?.summary.hold;
  const talk = isAsked(hold) ? holdSeriesHref(hold, seriesRunPath) : null;
  const question = attempt.pendingQuestion;
  if (!question) return null;
  if (lacks || talk) {
    return (
      <div className="q-item q-head" data-testid="conv-question-text">
        <div className="qt">{question.text}</div>
        <div className="qm">{talk ? <Link href={talk} data-testid="conv-question-series">{t("tickets.thread.answerInSeries")}</Link>
          : t("tickets.thread.answerNeedsControl", { n: attempt.number })}</div>
      </div>
    );
  }
  return (
    <div className="q-head" data-testid="conv-question">
      <PendingQuestionCard runId={attempt.runId} question={question} answered={answered}
        onAnswered={() => setAnswered(true)} label={t("tickets.thread.sendAndResume", { n: attempt.number })} />
    </div>
  );
}

export function DerivationMessage({ derivation }: { derivation: TicketDerivation }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <StoryRow line={derivationStoryLine(t, derivation)} open={open} onToggle={() => setOpen(!open)}
      testId={`spec-derivation-${derivation.runId}`} className={derivation.outcome === "not-finished" ? "stopped" : undefined}>
      <div className="tk-said" data-testid={`spec-derivation-outcome-${derivation.runId}`}>{t("tickets.thread.derivation", { words: derivationWords(t, derivation) })}</div>
      {derivation.summary && <div className="tk-said tk-dim">{derivation.summary}</div>}
      {derivation.pullRequests.map((url) => <a key={url} className="tk-link" href={url} target="_blank" rel="noreferrer">{t("tickets.thread.specPullRequest")}</a>)}
      <Link className="tk-runline" href={runPath(derivation.runId)}>{t("tickets.thread.derivationRun", { cost: money(derivation.cost) })}</Link>
    </StoryRow>
  );
}

/** A folded run of equal repeats: one line ("Runs 3–6 · 4× same cause"), the lines it holds opened on demand. */
export function RepeatLine({ items, testId, children }: { items: ThreadItem[]; testId: string; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  // 2026-11-22-7103a: the fold reads as its last run's status line, with that run's next step.
  const { ctx, queuePlace } = useStatusContext();
  const last = items.flatMap((i) => (i.kind === "report" ? [i.attempt] : [])).at(-1);
  const t = useT();
  const status = last ? attemptStatus(t, last, ctx, queuePlace) : undefined;
  return (
    <StoryRow line={foldStoryLine(t, items, status)} open={open} onToggle={() => setOpen(!open)} testId={testId} className="tk-fold">
      <div className="tk-fold-body">{children}</div>
    </StoryRow>
  );
}
