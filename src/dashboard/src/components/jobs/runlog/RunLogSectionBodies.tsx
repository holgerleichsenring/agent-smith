"use client";

import Link from "next/link";
import { useCallback, useMemo, useState } from "react";
import { ResultDocument } from "@/components/jobs/ResultTab";
import { VerifySummary } from "@/components/jobs/story/VerifySummary";
import type { RunJudgements } from "@/components/jobs/story/useRunJudgements";
import { buildVerifyFallback } from "@/components/jobs/story/verifyFallback";
import { Markdown } from "@/components/ui/Markdown";
import { usePlanMarkdown } from "@/hooks/usePlanMarkdown";
import { useResultMarkdown } from "@/hooks/useResultMarkdown";
import { useRunDecisions } from "@/hooks/useRunDecisions";
import { useRunSpecRecord } from "@/hooks/useRunSpecRecord";
import { useSpecMarkdown } from "@/hooks/useSpecMarkdown";
import { pullRequestStatusLabel } from "@/lib/prStatus";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import {
  EventType,
  type ProgressLedgerEntry,
  type PullRequestStatus,
  type RunEvent,
  type RunSnapshot,
  type TicketFetchedEvent,
} from "@/types/hub-events";
import { CitedList } from "./CitedList";
import { decisionParts } from "./decisionParts";
import { shortTargets } from "./logPaths";
import { BlockText, InlineText } from "./LogText";
import { parseSpecRecord } from "./specRecord";
import { SpecRecordView } from "./SpecRecordView";
import { clockOf, pullRequestLinks } from "./runNowLines";
import type { RunTicket } from "./useRunTicket";

// 2026-11-17-6d9eb: what a log section holds besides its steps, drawn plain: the ticket as the run read it; the work
// spec, the agreed plan, the progress ledger and (for a run without specs) its decisions; a spec's decisions and the
// spec it executed; the criteria with their overrule; the pull requests and the run report. Long documents sit behind
// a one-line disclosure so the log stays a log.

function Doc({ label, testId, children }: { label: string; testId: string; children: React.ReactNode }) {
  return (
    <details className="rl-doc" data-testid={testId}>
      <summary>{label}</summary>
      <div className="rl-doc-body">{children}</div>
    </details>
  );
}

export function TicketBody({ snapshot, events, ticket }: { snapshot: RunSnapshot | null; events: RunEvent[]; ticket: RunTicket | null }) {
  const fetched = useMemo(() => {
    let latest: TicketFetchedEvent | null = null;
    for (const e of events) if (e.type === EventType.TicketFetched) latest = e;
    return latest;
  }, [events]);
  const ratified = snapshot?.acceptance?.criteria ?? [];
  const id = fetched?.ticketId ?? snapshot?.ticketId ?? null;
  const title = fetched?.title ?? snapshot?.ticketTitle ?? null;
  return (
    <div className="rl-body" data-testid="ticket-body">
      {(id || title) && <div className="rl-body-line rl-strong" data-testid="ticket-body-title">{[id, title].filter(Boolean).join(" · ")}</div>}
      {fetched && (
        <>
          <div className="rl-body-line rl-dim" data-testid="ticket-body-facts">
            {fetched.state} · {fetched.source}
            {fetched.labels.length > 0 ? ` · labels ${fetched.labels.join(", ")}` : ""}
            {` · ${fetched.attachmentCount === 0 ? "no attachments" : fetched.attachmentCount === 1 ? "1 attachment, open the ticket in its tracker to view it" : `${fetched.attachmentCount} attachments, open the ticket in its tracker to view them`}`}
          </div>
          <Doc label="the ticket's text" testId="ticket-body-description">
            <Markdown>{fetched.description}</Markdown>
          </Doc>
          {fetched.acceptanceCriteria && (
            <Doc label="the ticket's acceptance criteria" testId="ticket-body-criteria"><Markdown>{fetched.acceptanceCriteria}</Markdown></Doc>
          )}
        </>
      )}
      {ratified.length > 0 && (
        <div data-testid="ticket-body-ratified">
          <div className="rl-body-line rl-dim">acceptance, ratified before any code</div>
          <ol className="rl-num">
            {ratified.map((c, i) => <li key={`${i}-${c.text}`} className="rl-body-line" data-testid="ticket-body-criterion"><span className="rl-num-n">{i + 1}.</span><span><InlineText text={c.text} /></span></li>)}
          </ol>
        </div>
      )}
      {!fetched && (
        <div className="rl-body-line rl-dim" data-testid="ticket-body-empty">
          body not recorded on this run{ticket ? <> — <Link href={ticket.href} data-testid="ticket-body-page">open the ticket</Link></> : ""}
        </div>
      )}
      {fetched && (
        <div className="rl-links">
          {fetched.webUrl && <a href={fetched.webUrl} target="_blank" rel="noreferrer" data-testid="ticket-body-tracker">open in the tracker ↗</a>}
          {ticket && <Link href={ticket.href} data-testid="ticket-body-page">the ticket page</Link>}
        </div>
      )}
    </div>
  );
}

const LEDGER_TAG: Record<ProgressLedgerEntry["status"], string> = { done: "done", in_progress: "now", pending: "next" };

export function PlanBody({ runId, ledger, hasSpecs, events }: { runId: string; ledger: ProgressLedgerEntry[] | null; hasSpecs: boolean; events: RunEvent[] }) {
  const spec = useSpecMarkdown(runId);
  const plan = usePlanMarkdown(runId);
  return (
    <div className="rl-body" data-testid="plan-body">
      {spec.content && <Doc label="the work spec" testId="plan-body-spec"><ResultDocument content={spec.content} /></Doc>}
      {plan.content ? (
        <Doc label="the agreed plan" testId="plan-body-plan"><ResultDocument content={plan.content} /></Doc>
      ) : (
        <div className="rl-body-line rl-dim" data-testid="plan-body-no-plan">
          {plan.loading ? "Reading the plan…" : "No plan recorded yet; the master writes plan.md during the run."}
        </div>
      )}
      {ledger && ledger.length > 0 && (
        <div data-testid="plan-body-ledger">
          {ledger.map((e) => (
            <div key={e.id} className={`rl-body-line rl-ledger ${e.status}`} data-testid={`ledger-line-${e.id}`} data-status={e.status}>
              <span className="rl-ledger-tag">{LEDGER_TAG[e.status]}</span>
              <span className="rl-ledger-act" title={e.activity}><InlineText text={e.activity} /></span>
              {e.target ? <span className="rl-ledger-target" title={e.target} data-testid={`ledger-target-${e.id}`}>{shortTargets(e.target)}</span> : <span />}
            </div>
          ))}
          <div className="rl-body-line rl-dim" data-testid="plan-body-ledger-count">{ledgerCount(ledger)}</div>
        </div>
      )}
      {!hasSpecs && <RunDecisions runId={runId} events={events} />}
    </div>
  );
}

export function ledgerCount(ledger: ProgressLedgerEntry[]): string {
  const done = ledger.filter((e) => e.status === "done").length;
  const now = ledger.filter((e) => e.status === "in_progress").length;
  return `${done} done · ${now} now · ${ledger.length - done - now} to go`;
}

function RunDecisions({ runId, events }: { runId: string; events: RunEvent[] }) {
  const decisions = useRunDecisions(runId, events.length);
  if (decisions.length === 0) return null;
  return (
    <div data-testid="plan-body-decisions">
      <div className="rl-body-line rl-dim">decisions</div>
      {decisions.map((d, i) => (
        <DecisionLine key={`${d.name}-${i}`} name={d.name} reason={d.reason} prefix={`${clockOf(d.recordedAt) ?? ""}${d.category ? ` ${d.category}` : ""}`} />
      ))}
    </div>
  );
}

/** 2026-11-18-e06bb: one decision on one line — the title bold, the reason dim and cut at the line's end; a click
 *  opens the whole reason under it. */
export function DecisionLine({ name, reason, prefix }: { name: string; reason: string | null; prefix?: string }) {
  const [open, setOpen] = useState(false);
  const parts = decisionParts(name, reason);
  return (
    <div className="rl-decision" data-testid="decision-line">
      <button type="button" className="rl-decision-line" aria-expanded={open} disabled={!parts.reason} onClick={() => setOpen((o) => !o)}>
        {prefix?.trim() && <span className="rl-dim rl-decision-pre">{prefix.trim()}</span>}
        <strong className="rl-decision-title"><InlineText text={parts.title} /></strong>
        {parts.reason && !open && <span className="rl-decision-reason" data-testid="decision-reason"> — <InlineText text={parts.reason} /></span>}
      </button>
      {open && parts.reason && <div className="rl-decision-full" data-testid="decision-full"><BlockText text={parts.reason} /></div>}
    </div>
  );
}

export function SpecBody({ runId, spec }: { runId: string; spec: RunSpecRow | undefined }) {
  const [read, setRead] = useState(false);
  if (!spec) return null;
  return (
    <div className="rl-body" data-testid={`spec-body-${spec.specId}`}>
      {spec.decisions.length > 0 ? (
        <Doc label={spec.decisions.length === 1 ? "1 decision" : `${spec.decisions.length} decisions`} testId={`spec-decisions-${spec.specId}`}>
          {spec.decisions.map((d, i) => <DecisionLine key={`${d.name}-${i}`} name={d.name} reason={d.reason} />)}
        </Doc>
      ) : (
        <div className="rl-body-line rl-dim">No decision was logged in this spec.</div>
      )}
      <details className="rl-doc" data-testid={`spec-record-${spec.specId}`} onToggle={(e) => setRead((e.target as HTMLDetailsElement).open)}>
        <summary>the spec it executed</summary>
        {read && <SpecRecord runId={runId} specId={spec.specId} />}
      </details>
    </div>
  );
}

function SpecRecord({ runId, specId }: { runId: string; specId: string }) {
  const { record, loading } = useRunSpecRecord(runId, specId);
  return (
    <div className="rl-doc-body">
      {record ? <SpecRecordBody record={record} /> : (
        <div className="rl-body-line rl-dim">
          {loading ? "Reading…" : `No executed spec recorded for ${specId}; this run wrote none, or it predates the server-held spec record.`}
        </div>
      )}
    </div>
  );
}

/** 2026-11-18-e06bc: the record as labelled lines when it parses as a spec, else as the document it is. */
function SpecRecordBody({ record }: { record: string }) {
  const spec = useMemo(() => parseSpecRecord(record), [record]);
  return spec ? <SpecRecordView spec={spec} /> : <ResultDocument content={record} />;
}

export function CheckBody({ snapshot, events, judged }: { snapshot: RunSnapshot | null; events: RunEvent[]; judged: RunJudgements }) {
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  return (
    <div className="rl-body rl-plain" data-testid="check-body">
      <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} judgements={judged.judgements}
        judgementBusy={judged.busy} onRecordJudgement={judged.record} onWithdrawJudgement={judged.withdraw}
        renderCitation={(citation) => <CitedList text={citation} />} />
      {judged.error && <div className="rl-body-line rl-bad" data-testid="check-body-error">The judgement was not recorded: {judged.error}</div>}
    </div>
  );
}

export function DeliveredBody({ runId, snapshot }: { runId: string; snapshot: RunSnapshot | null }) {
  const prs = snapshot ? pullRequestLinks(snapshot) : [];
  const statuses = new Map((snapshot?.pullRequests ?? []).map((p) => [p.url, p.status]));
  return (
    <div className="rl-body" data-testid="delivered-body">
      {prs.map((p) => {
        const status = statuses.get(p.url);
        return (
          <div key={p.url} className="rl-body-line" data-testid="delivered-pr">
            {p.repo ? `${p.repo} · ` : ""}{status ? `${pullRequestStatusLabel(status as PullRequestStatus)} · ` : ""}
            <a href={p.url} target="_blank" rel="noreferrer">{p.isDraft ? "draft pull request" : "pull request"} ↗</a>
          </div>
        );
      })}
      <RunReport runId={runId} />
    </div>
  );
}

function RunReport({ runId }: { runId: string }) {
  const { content, loading, error } = useResultMarkdown(runId);
  const [copied, setCopied] = useState(false);
  const copy = useCallback(async () => {
    if (!content) return;
    try {
      await navigator.clipboard.writeText(content);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch { /* clipboard blocked */ }
  }, [content]);
  if (loading) return <div className="rl-body-line rl-dim">Reading the run report…</div>;
  if (error) return <div className="rl-body-line rl-bad" data-testid="delivered-report-error">The run report could not be read: {error}</div>;
  if (!content) return <div className="rl-body-line rl-dim" data-testid="delivered-report-none">No run report yet; it is written when the run finishes.</div>;
  return (
    <Doc label="the run report" testId="delivered-report">
      <button type="button" className="rl-link" onClick={copy} data-testid="delivered-report-copy">{copied ? "copied" : "copy the markdown"}</button>
      <ResultDocument content={content} />
    </Doc>
  );
}
