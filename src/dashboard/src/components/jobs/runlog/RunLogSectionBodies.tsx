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
import { useDateLocale, useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";
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
import { BlockText, InlineText, LogDoc } from "./LogText";
import { parseSpecRecord } from "./specRecord";
import { SpecRecordView } from "./SpecRecordView";
import type { StepPass } from "./specPasses";
import { specRevisions } from "./specRevisions";
import { StepSpecSteps } from "./StepSpecSteps";
import type { GivenPasses } from "./useSpecStepLines";
import { clockOf, pullRequestLinks } from "./runNowLines";
import type { RunTicket } from "./useRunTicket";

// 2026-11-17-6d9eb: what a log section holds besides its steps, drawn plain: the ticket as the run read it; the work
// spec, the agreed plan, the progress ledger and (for a run without specs) its decisions; a spec's decisions and the
// spec it executed; the criteria with their overrule; the pull requests and the run report. Long documents sit behind
// a one-line disclosure so the log stays a log. 2026-11-26-5201g: a spec's section also holds its steps, once.

const Doc = LogDoc;

export function TicketBody({ snapshot, events, ticket }: { snapshot: RunSnapshot | null; events: RunEvent[]; ticket: RunTicket | null }) {
  const t = useT();
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
            {fetched.labels.length > 0 ? t("runs.log.body.labels", { labels: fetched.labels.join(", ") }) : ""}
            {` · ${fetched.attachmentCount === 0 ? t("runs.log.body.noAttachments") : t("runs.log.body.attachments", { count: fetched.attachmentCount })}`}
          </div>
          <Doc label={t("runs.log.body.ticketText")} testId="ticket-body-description">
            <Markdown>{fetched.description}</Markdown>
          </Doc>
          {fetched.acceptanceCriteria && (
            <Doc label={t("runs.log.body.ticketCriteria")} testId="ticket-body-criteria"><Markdown>{fetched.acceptanceCriteria}</Markdown></Doc>
          )}
        </>
      )}
      {ratified.length > 0 && (
        <div data-testid="ticket-body-ratified">
          <div className="rl-body-line rl-dim">{t("runs.log.body.ratified")}</div>
          <ol className="rl-num">
            {ratified.map((c, i) => <li key={`${i}-${c.text}`} className="rl-body-line" data-testid="ticket-body-criterion"><span className="rl-num-n">{i + 1}.</span><span><InlineText text={c.text} /></span></li>)}
          </ol>
        </div>
      )}
      {!fetched && (
        <div className="rl-body-line rl-dim" data-testid="ticket-body-empty">
          {t("runs.log.body.notRecorded")}{ticket ? <> — <Link href={ticket.href} data-testid="ticket-body-page">{t("runs.log.body.openTicket")}</Link></> : ""}
        </div>
      )}
      {fetched && (
        <div className="rl-links">
          {fetched.webUrl && <a href={fetched.webUrl} target="_blank" rel="noreferrer" data-testid="ticket-body-tracker">{t("runs.log.body.openTracker")}</a>}
          {ticket && <Link href={ticket.href} data-testid="ticket-body-page">{t("runs.log.body.ticketPage")}</Link>}
        </div>
      )}
    </div>
  );
}

const LEDGER_TAG: Record<ProgressLedgerEntry["status"], TextKey> = {
  done: "runs.log.ledger.done", in_progress: "runs.log.ledger.in_progress", pending: "runs.log.ledger.pending",
};

export function PlanBody({ runId, ledger, hasSpecs, events }: { runId: string; ledger: ProgressLedgerEntry[] | null; hasSpecs: boolean; events: RunEvent[] }) {
  const t = useT();
  const spec = useSpecMarkdown(runId);
  const plan = usePlanMarkdown(runId);
  return (
    <div className="rl-body" data-testid="plan-body">
      {spec.content && <Doc label={t("runs.log.plan.workSpec")} testId="plan-body-spec"><ResultDocument content={spec.content} /></Doc>}
      {plan.content ? (
        <Doc label={t("runs.log.plan.agreed")} testId="plan-body-plan"><ResultDocument content={plan.content} /></Doc>
      ) : (
        <div className="rl-body-line rl-dim" data-testid="plan-body-no-plan">
          {t(plan.loading ? "runs.log.plan.reading" : "runs.log.plan.noPlan")}
        </div>
      )}
      {ledger && ledger.length > 0 && (
        <div data-testid="plan-body-ledger">
          {ledger.map((e) => (
            <div key={e.id} className={`rl-body-line rl-ledger ${e.status}`} data-testid={`ledger-line-${e.id}`} data-status={e.status}>
              <span className="rl-ledger-tag">{t(LEDGER_TAG[e.status])}</span>
              <span className="rl-ledger-act" title={e.activity}><InlineText text={e.activity} /></span>
              {e.target ? <span className="rl-ledger-target" title={e.target} data-testid={`ledger-target-${e.id}`}><bdi>{shortTargets(e.target)}</bdi></span> : <span />}
            </div>
          ))}
          <div className="rl-body-line rl-dim" data-testid="plan-body-ledger-count">{ledgerCount(t, ledger)}</div>
        </div>
      )}
      {!hasSpecs && <RunDecisions runId={runId} events={events} />}
    </div>
  );
}

export function ledgerCount(t: T, ledger: ProgressLedgerEntry[]): string {
  const done = ledger.filter((e) => e.status === "done").length;
  const now = ledger.filter((e) => e.status === "in_progress").length;
  return t("runs.log.ledger.count", { done, now, left: ledger.length - done - now });
}

function RunDecisions({ runId, events }: { runId: string; events: RunEvent[] }) {
  const t = useT();
  const locale = useDateLocale();
  const decisions = useRunDecisions(runId, events.length);
  if (decisions.length === 0) return null;
  return (
    <div data-testid="plan-body-decisions">
      <div className="rl-body-line rl-dim">{t("runs.log.decisions")}</div>
      {decisions.map((d, i) => (
        <DecisionLine key={`${d.name}-${i}`} name={d.name} reason={d.reason} prefix={`${clockOf(d.recordedAt, locale) ?? ""}${d.category ? ` ${d.category}` : ""}`} />
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

/** 2026-11-26-5201g: what a spec's steps list reads — the run's ledger and whether it is this spec's, the run's passes as
 *  the page holds them, and the master pass of each step index. */
export interface SpecBodySteps {
  ledger: ProgressLedgerEntry[] | null;
  ledgerIsThisSpec: boolean;
  passes?: GivenPasses;
  masterPasses: ReadonlyMap<number, StepPass>;
}

export function SpecBody({ runId, spec, steps }: { runId: string; spec: RunSpecRow | undefined; steps?: SpecBodySteps }) {
  const t = useT();
  const [read, setRead] = useState(false);
  const masterPasses = steps?.masterPasses;
  const revisions = useMemo(() => (spec && masterPasses ? specRevisions(spec.decisions, masterPasses) : []), [spec, masterPasses]);
  if (!spec) return null;
  return (
    <div className="rl-body" data-testid={`spec-body-${spec.specId}`}>
      {/* 2026-11-22-7103a: why a stopped spec was not delivered, as the server wrote it, whole. */}
      {spec.verdict && (spec.status === "failed" || spec.status === "handed_back") && (
        <div className="rl-body-line rl-bad" data-testid={`spec-body-verdict-${spec.specId}`}>
          {t(spec.status === "handed_back" ? "runs.log.spec.handedBack" : "runs.log.spec.notDelivered", { verdict: spec.verdict })}
        </div>
      )}
      {steps && (
        <StepSpecSteps runId={runId} specId={spec.specId} ledger={steps.ledger} ledgerIsThisSpec={steps.ledgerIsThisSpec}
          passes={steps.passes} revisions={revisions} />
      )}
      {spec.decisions.length > 0 ? (
        <Doc label={t("runs.log.decisionsCount", { count: spec.decisions.length })} testId={`spec-decisions-${spec.specId}`}>
          {spec.decisions.map((d, i) => <DecisionLine key={`${d.name}-${i}`} name={d.name} reason={d.reason} />)}
        </Doc>
      ) : (
        <div className="rl-body-line rl-dim">{t("runs.log.spec.noDecision")}</div>
      )}
      <details className="rl-doc" data-testid={`spec-record-${spec.specId}`} onToggle={(e) => setRead((e.target as HTMLDetailsElement).open)}>
        <summary>{t("runs.log.spec.executed")}</summary>
        {read && <SpecRecord runId={runId} specId={spec.specId} />}
      </details>
    </div>
  );
}

function SpecRecord({ runId, specId }: { runId: string; specId: string }) {
  const t = useT();
  const { record, loading } = useRunSpecRecord(runId, specId);
  return (
    <div className="rl-doc-body">
      {record ? <SpecRecordBody record={record} /> : (
        <div className="rl-body-line rl-dim">
          {loading ? t("runs.log.spec.reading") : t("runs.log.spec.noRecord", { spec: specId })}
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
  const t = useT();
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  return (
    <div className="rl-body rl-plain" data-testid="check-body">
      <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} judgements={judged.judgements}
        judgementBusy={judged.busy} onRecordJudgement={judged.record} onWithdrawJudgement={judged.withdraw}
        renderCitation={(citation) => <CitedList text={citation} />} renderText={(text) => <InlineText text={text} />} />
      {judged.error && <div className="rl-body-line rl-bad" data-testid="check-body-error">{t("runs.log.check.judgementFailed", { error: judged.error })}</div>}
    </div>
  );
}

export function DeliveredBody({ runId, snapshot }: { runId: string; snapshot: RunSnapshot | null }) {
  const t = useT();
  const prs = snapshot ? pullRequestLinks(snapshot) : [];
  const statuses = new Map((snapshot?.pullRequests ?? []).map((p) => [p.url, p.status]));
  return (
    <div className="rl-body" data-testid="delivered-body">
      {prs.map((p) => {
        const status = statuses.get(p.url);
        return (
          <div key={p.url} className="rl-body-line" data-testid="delivered-pr">
            {p.repo ? `${p.repo} · ` : ""}{status ? `${pullRequestStatusLabel(status as PullRequestStatus, t)} · ` : ""}
            <a href={p.url} target="_blank" rel="noreferrer">{t(p.isDraft ? "runs.log.pr.draft" : "runs.log.pr.ready")} ↗</a>
          </div>
        );
      })}
      <RunReport runId={runId} />
    </div>
  );
}

function RunReport({ runId }: { runId: string }) {
  const t = useT();
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
  if (loading) return <div className="rl-body-line rl-dim">{t("runs.log.report.reading")}</div>;
  if (error) return <div className="rl-body-line rl-bad" data-testid="delivered-report-error">{t("runs.log.report.failed", { error })}</div>;
  if (!content) return <div className="rl-body-line rl-dim" data-testid="delivered-report-none">{t("runs.log.report.none")}</div>;
  return (
    <Doc label={t("runs.log.report.label")} testId="delivered-report">
      <button type="button" className="rl-link" onClick={copy} data-testid="delivered-report-copy">{t(copied ? "runs.log.report.copied" : "runs.log.report.copy")}</button>
      <ResultDocument content={content} />
    </Doc>
  );
}
