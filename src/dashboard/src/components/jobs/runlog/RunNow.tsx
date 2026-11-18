"use client";

import Link from "next/link";
import { useMemo } from "react";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { RetryUnchanged } from "@/components/tickets/detail/RetryUnchanged";
import { useSandboxActivity } from "@/hooks/useSandboxActivity";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { endedView, liveLines, nowLabel, nowMode, pullRequestLinks, waitLines, type NowLine } from "./runNowLines";
import type { RunTicket } from "./useRunTicket";
import { VerifyPassLines } from "./VerifyPassLines";

// 2026-11-17-6d9ea: the run page's Now block, between two hairlines. A working run's last few acts, read off the hub's
// run events as they arrive; a waiting run's question with its answer form; a finished run's "How it ended" with the
// acts that fit it: its pull requests, Retry unchanged, the setting that lifts a stop, the ticket and the Why page.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  steps: RunStepRow[];
  ticket: RunTicket | null;
}

export function RunNow({ runId, snapshot, events, steps, ticket }: Props) {
  const beat = useSandboxActivity(snapshot?.runId ?? null);
  const mode = snapshot ? nowMode(snapshot) : null;
  const view = useMemo(() => {
    if (!snapshot || !mode) return null;
    if (mode === "ended") return endedView(snapshot, steps, events, ticket?.attempt ?? null);
    if (mode === "live") return { lines: liveLines(snapshot, events, steps, beat), verify: null, settingsPath: null };
    return { lines: waitLines(snapshot), verify: null, settingsPath: null };
  }, [snapshot, mode, steps, events, ticket?.attempt, beat]);
  if (!snapshot || !view || !mode) return null;
  const question = mode === "waiting" ? snapshot.pendingQuestion ?? null : null;
  const prs = mode === "ended" ? pullRequestLinks(snapshot) : [];
  return (
    <section className="rl-now" data-testid="run-now" data-mode={mode} aria-live="polite">
      <span className="rl-lbl" data-testid="run-now-label">{nowLabel(snapshot)}</span>
      {view.lines.map((l) => <Line key={l.key} line={l} />)}
      {view.verify && (
        <div className="rl-now-line" data-testid="run-now-verify">
          <span className="rl-at" />
          <span className="rl-kind">verify</span>
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
              {p.isDraft ? "draft pull request" : "pull request"}{prs.length > 1 && p.repo ? ` ${p.repo}` : ""} ↗
            </a>
          ))}
          {ticket?.newest && <RetryUnchanged attempt={ticket.attempt} project={ticket.project} />}
          {view.settingsPath && <Link href={view.settingsPath} data-testid="run-now-setting">open the setting</Link>}
          {ticket && <Link href={ticket.href} data-testid="run-now-ticket">the ticket</Link>}
          <Link href={runTracePath(runId)} data-testid="run-now-why">why it went this way</Link>
        </div>
      )}
    </section>
  );
}

function Line({ line }: { line: NowLine }) {
  return (
    <div className="rl-now-line" data-testid="run-now-line" data-kind={line.kind} data-tone={line.tone}>
      <span className="rl-at">{line.at ?? ""}</span>
      <span className="rl-kind" title={line.kind}>{line.kind}</span>
      <span className="rl-text">{line.text}</span>
    </div>
  );
}
