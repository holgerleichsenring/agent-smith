"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { RetryUnchanged, type RetrySettled } from "@/components/tickets/detail/RetryUnchanged";
import { useSandboxActivity } from "@/hooks/useSandboxActivity";
import { useDateLocale, useT } from "@/hooks/useT";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runPath, runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { clockOf, endedView, kindWord, liveLines, nowLabel, nowMode, pullRequestLinks, waitLines, type NowLine } from "./runNowLines";
import { statusJoin, statusRest, type RunStatusLine } from "./runStatusLine";
import type { RunTicket } from "./useRunTicket";
import { VerifyPassLines } from "./VerifyPassLines";

// 2026-11-17-6d9ea: the run page's Now block, between two hairlines. A working run's last few acts, read off the hub's
// run events as they arrive; a waiting run's question with its answer form; a finished run's "How it ended" with the
// acts that fit it: its pull requests, Retry unchanged, the setting that lifts a stop, the ticket and the Why page.
// 2026-11-18-d6d2d: Retry only on the ticket's latest attempt; an older one links to the latest instead. A Retry this
// page requested follows the new attempt once the ticket names it; a refusal reads the ticket again.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  /** 2026-11-22-7103a: the run's status line, How it ended's first line. */
  status?: RunStatusLine | null;
  events: RunEvent[];
  steps: RunStepRow[];
  ticket: RunTicket | null;
}

export function RunNow({ runId, snapshot, status, events, steps, ticket }: Props) {
  const t = useT();
  const locale = useDateLocale();
  const beat = useSandboxActivity(snapshot?.runId ?? null);
  const follow = useRetryFollow(runId, ticket);
  const mode = snapshot ? nowMode(snapshot) : null;
  const view = useMemo(() => {
    if (!snapshot || !mode) return null;
    if (mode === "ended") return endedView(t, snapshot, steps, events, ticket?.attempt ?? null);
    if (mode === "live") return { lines: liveLines(t, locale, snapshot, events, steps, beat), verify: null, settingsPath: null, said: null };
    return { lines: waitLines(t, locale, snapshot), verify: null, settingsPath: null, said: null };
  }, [t, locale, snapshot, mode, steps, events, ticket?.attempt, beat]);
  if (!snapshot || !view || !mode) return null;
  const question = mode === "waiting" ? snapshot.pendingQuestion ?? null : null;
  const prs = mode === "ended" ? pullRequestLinks(snapshot) : [];
  return (
    <section className="rl-now" data-testid="run-now" data-mode={mode} aria-live="polite">
      <span className="rl-lbl" data-testid="run-now-label">{nowLabel(t, snapshot)}</span>
      {mode === "ended" && status && <StatusLine line={status} at={clockOf(snapshot.finishedAt, locale)} />}
      {view.lines.map((l) => <Line key={l.key} line={l} />)}
      {view.said && (
        <details className="rl-doc" data-testid="run-now-said">
          <summary>{t("runs.log.now.ownWords")}</summary>
          <div className="rl-doc-body rl-said">{view.said}</div>
        </details>
      )}
      {view.verify && (
        <div className="rl-now-line" data-testid="run-now-verify">
          <span className="rl-at" />
          <span className="rl-kind">{kindWord(t, "verify")}</span>
          <VerifyPassLines pass={view.verify} />
        </div>
      )}
      {question && (
        <div className="rl-question" data-testid="run-question">
          <PendingQuestionCard runId={runId} question={question} />
        </div>
      )}
      {mode === "ended" && (
        <div className="rl-now-acts" data-testid="run-now-acts">
          {prs.map((p) => (
            <a key={p.url} href={p.url} target="_blank" rel="noreferrer" data-testid="run-now-pr">
              {t(p.isDraft ? "runs.log.pr.draft" : "runs.log.pr.ready")}{prs.length > 1 && p.repo ? ` ${p.repo}` : ""} ↗
            </a>
          ))}
          {ticket?.newest && <RetryUnchanged attempt={ticket.attempt} project={ticket.project} onAnswer={follow} />}
          {ticket && !ticket.newest && ticket.latest && ticket.latest.runId !== runId && (
            <Link href={runPath(ticket.latest.runId)} data-testid="run-now-latest">
              {t("runs.log.now.latest", { number: ticket.latest.number })}
            </Link>
          )}
          {view.settingsPath && <Link href={view.settingsPath} data-testid="run-now-setting">{t("runs.log.now.setting")}</Link>}
          {ticket && <Link href={ticket.href} data-testid="run-now-ticket">{t("runs.log.now.ticket")}</Link>}
          <Link href={runTracePath(runId)} data-testid="run-now-why">{t("runs.log.now.why")}</Link>
        </div>
      )}
    </section>
  );
}

/** After a Retry this page requested, opens the ticket's newer attempt once a re-read names it; a refusal re-reads. */
function useRetryFollow(runId: string, ticket: RunTicket | null): (settled: RetrySettled) => void {
  const router = useRouter();
  const [requested, setRequested] = useState(false);
  const target = requested && ticket?.latest && ticket.latest.runId !== runId ? ticket.latest.runId : null;
  useEffect(() => { if (target) router.push(runPath(target)); }, [target, router]);
  return (settled) => (settled === "requested" ? setRequested(true) : ticket?.reread());
}

/** 2026-11-22-7103a: How it ended's first line — the head in the run's colour, the clauses and next step in plain ink. */
function StatusLine({ line, at }: { line: RunStatusLine; at: string | null }) {
  const t = useT();
  const rest = statusRest(line);
  return (
    <div className="rl-now-line rl-status" data-testid="run-status-line" data-kind="outcome">
      <span className="rl-at">{at ?? ""}</span>
      <span className="rl-kind">{kindWord(t, "outcome")}</span>
      <span className="rl-text">
        <span className="rl-status-head" data-testid="run-status-head" data-tone={line.tone}>{line.head}</span>
        {rest && `${statusJoin(line.head)}${rest}`}
      </span>
    </div>
  );
}

function Line({ line, testId = "run-now-line" }: { line: NowLine; testId?: string }) {
  const t = useT();
  return (
    <div className="rl-now-line" data-testid={testId} data-kind={line.kind} data-tone={line.tone}>
      <span className="rl-at">{line.at ?? ""}</span>
      <span className="rl-kind" title={kindWord(t, line.kind)}>{kindWord(t, line.kind)}</span>
      <span className="rl-text">{line.text}</span>
    </div>
  );
}
