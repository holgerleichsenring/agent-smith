"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { AcceptanceCriterion, RunAcceptance } from "@/types/hub-events";
import type { VerifyFallbackView } from "./verifyFallback";
import { CriterionJudgementControl } from "./CriterionJudgementControl";
import { UnmeasuredAccounts } from "./UnmeasuredAccounts";
import { BlockText } from "@/components/jobs/runlog/LogText";
import type { CriterionDisposition, CriterionJudgement } from "@/lib/judgementsApi";

// p0344b: the Verify beat's surface. Preferred source is the run's PERSISTED
// per-criterion acceptance dispositions (snapshot.acceptance — the p0340
// keystone verdicts). Runs persisted before that field fall back to the p0328
// ExpectationRatified event view — and a run with neither renders an honest
// "nothing ratified" empty state. Never a fabricated green.
// p0343c (pixel identity): emits the run-viewer.html verify DOM verbatim —
// a .card with the "Verify against the ratified acceptance" header and .crit
// rows (pass/fail/wait) with c-mark, c-txt, c-proof and c-stat.

const OUTCOME_LABEL: Record<string, string> = {
  verbatim: "Ratified verbatim",
  edited: "Ratified with edits",
  rejected: "Rejected",
  unratified: "Unratified",
  none: "No ratified contract",
};

const CHECK = (
  <svg viewBox="0 0 16 16" width="14" height="14" fill="none" aria-hidden="true">
    <path
      d="M3.5 8.5l3 3 6-7"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

const CROSS = (
  <svg viewBox="0 0 16 16" width="14" height="14" fill="none" aria-hidden="true">
    <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
  </svg>
);

interface VerifySummaryProps {
  /** Persisted per-criterion dispositions; null/undefined on pre-p0344b runs. */
  acceptance: RunAcceptance | null | undefined;
  /** 2026-08-25-e257: the operator's judgements of these dispositions, served with them. */
  judgements?: CriterionJudgement[];
  onRecordJudgement?: (
    criterion: string,
    machineStatus: CriterionDisposition,
    humanStatus: CriterionDisposition,
    reason: string,
  ) => void;
  onWithdrawJudgement?: (criterion: string) => void;
  judgementBusy?: boolean;
  /** Event-derived legacy view — used ONLY when acceptance is absent. */
  fallback: VerifyFallbackView;
  /** 2026-11-18-e06bc: how a criterion's citation is drawn after "cited:"; the text itself when absent. */
  renderCitation?: (citation: string) => ReactNode;
  /** 2026-11-18-e06bc: how a criterion's text and reason are drawn; the plain text when absent. */
  renderText?: (text: string) => ReactNode;
}

export function VerifySummary({
  acceptance,
  fallback,
  judgements,
  onRecordJudgement,
  onWithdrawJudgement,
  judgementBusy,
  renderCitation,
  renderText,
}: VerifySummaryProps) {
  return acceptance ? (
    <AcceptanceVerify
      acceptance={acceptance}
      renderCitation={renderCitation}
      renderText={renderText}
      judgements={judgements}
      onRecordJudgement={onRecordJudgement}
      onWithdrawJudgement={onWithdrawJudgement}
      judgementBusy={judgementBusy}
    />
  ) : (
    <FallbackVerify view={fallback} />
  );
}

// --- Preferred: persisted per-criterion dispositions ------------------------

const CRIT_CLASS: Record<AcceptanceCriterion["status"], "pass" | "fail" | "wait"> = {
  met: "pass",
  unmet: "fail",
  not_applicable: "wait",
  unproven: "wait",
};

const CRIT_STAT: Record<AcceptanceCriterion["status"], string> = {
  met: "proven",
  unmet: "failed",
  not_applicable: "n/a",
  unproven: "unproven",
};

// 2026-10-12-6bfad: an unmeasured account is never one of the N; it makes the badge bad
// and says so after the count, or alone when there is nothing to count.
function badgeFor(acceptance: RunAcceptance): { cls: string; label: string } {
  // 2026-11-18-98fa: a run whose masters ended with finish_spec judged no line — the badge says it reported.
  if (acceptance.reports != null && acceptance.criteria.length === 0 && !acceptance.unmeasured?.length) {
    const followUps = acceptance.followUps?.length ?? 0;
    return { cls: "ok", label: followUps > 0 ? `reported · ${followUps} follow-up${followUps === 1 ? "" : "s"}` : "reported" };
  }
  const counted = criteriaBadge(acceptance);
  if (!acceptance.unmeasured?.length) return counted;
  return {
    cls: "bad",
    label: acceptance.criteria.length > 0 ? `${counted.label} · not measured` : "not measured",
  };
}

function criteriaBadge(acceptance: RunAcceptance): { cls: string; label: string } {
  const total = acceptance.criteria.length;
  if (total === 0) return { cls: "neu", label: OUTCOME_LABEL[acceptance.outcome ?? "none"] ?? "no criteria" };
  const met = acceptance.criteria.filter((c) => c.status === "met").length;
  const unmet = acceptance.criteria.filter((c) => c.status === "unmet").length;
  if (unmet > 0) return { cls: "bad", label: `${met} of ${total} · ${unmet} failed` };
  if (met === total) return { cls: "ok", label: `${met} of ${total} proven` };
  return { cls: "neu", label: `${met} of ${total} proven` };
}

function AcceptanceVerify({
  acceptance,
  judgements,
  onRecordJudgement,
  onWithdrawJudgement,
  judgementBusy,
  renderCitation,
  renderText = (text: string) => text,
}: {
  acceptance: RunAcceptance;
  renderCitation?: (citation: string) => ReactNode;
  renderText?: (text: string) => ReactNode;
  judgements?: CriterionJudgement[];
  onRecordJudgement?: VerifySummaryProps["onRecordJudgement"];
  onWithdrawJudgement?: VerifySummaryProps["onWithdrawJudgement"];
  judgementBusy?: boolean;
}) {
  const badge = badgeFor(acceptance);
  const unmeasured = acceptance.unmeasured ?? [];
  // 2026-11-18-c8e2d: a run whose rows are its masters' reports carries the report's lists (maybe empty).
  const report = acceptance.fixedInPassing != null || acceptance.followUps != null || acceptance.reports != null;
  // 2026-11-18-98fa: the masters' own markdown reports, rendered as written — no row is judged on such a run.
  const reports = acceptance.reports ?? [];
  return (
    <section className="card" data-testid="verify-summary" data-source="acceptance">
      <div className="card-h">
        <h3>{report ? "The master's report" : "Verify against the ratified acceptance"}</h3>
        <span className={cn("badge", badge.cls)} data-testid="verify-outcome-badge">
          {badge.label}
        </span>
      </div>
      <div className="card-b">
        <div className="hint" style={{ marginBottom: 6 }} data-testid="verify-source">
          {reports.length > 0 || (acceptance.reports != null && acceptance.criteria.length === 0) ? (
            <>
              What <b>each spec&apos;s master reported</b> after looking at what its sub-agent built, in its own words.
            </>
          ) : report ? (
            <>
              Each done line as <b>the spec&apos;s master reported it</b> after looking at what its sub-agent built —
              met, not applicable, or left with its why.
            </>
          ) : acceptance.source === "master_verification" ? (
            <>
              Each criterion the requester agreed to, as <b>the agent reported on its own
              work</b>. This is not the independent read the gate uses.
            </>
          ) : (
            <>
              Each ratified criterion, checked against the <b>real diff and test run</b> by a
              reader with no account of the work — this is what the gate decided on.
            </>
          )}
        </div>
        {unmeasured.length > 0 && <UnmeasuredAccounts accounts={unmeasured} />}
        {reports.map((r, i) => (
          <div key={i} data-testid="verify-report" style={{ marginTop: i > 0 ? 12 : 0 }}>
            {reports.length > 1 && r.specId && <p className="hint"><b>Spec {r.specId}</b></p>}
            <BlockText text={r.markdown} />
          </div>
        ))}
        {acceptance.criteria.length === 0 ? (
          unmeasured.length === 0 && acceptance.reports == null && (
            <p className="hint" data-testid="verify-empty">
              No acceptance criteria were recorded on this run — nothing has been proven green.
            </p>
          )
        ) : (
          <div data-testid="verify-criteria">
            {acceptance.criteria.map((criterion, i) => {
              const cls = CRIT_CLASS[criterion.status];
              return (
                <div
                  key={i}
                  className={cn("crit", cls)}
                  data-testid="verify-criterion"
                  data-status={criterion.status}
                >
                  <div className="c-mark">
                    {cls === "pass" ? CHECK : cls === "fail" ? CROSS : "?"}
                  </div>
                  <div>
                    <div className="c-txt">{renderText(criterion.text)}</div>
                    {criterion.reason && (
                      <div className="c-proof" data-testid="verify-criterion-reason">
                        {renderText(criterion.reason)}
                      </div>
                    )}
                    {criterion.citation && (
                      <div className="c-proof" data-testid="verify-criterion-citation">
                        cited: {renderCitation ? renderCitation(criterion.citation) : criterion.citation}
                      </div>
                    )}
                    {onRecordJudgement && onWithdrawJudgement && (
                      <CriterionJudgementControl
                        criterion={criterion.text}
                        machineStatus={criterion.status}
                        judgement={judgements?.find((j) => j.criterion === criterion.text)}
                        busy={judgementBusy}
                        onRecord={(humanStatus, reason) =>
                          onRecordJudgement(
                            criterion.text, criterion.status, humanStatus, reason)
                        }
                        onWithdraw={() => onWithdrawJudgement(criterion.text)}
                      />
                    )}
                  </div>
                  <div className="c-stat">{CRIT_STAT[criterion.status]}</div>
                </div>
              );
            })}
          </div>
        )}
        {acceptance.declined && acceptance.declined.length > 0 && (
          <div data-testid="verify-declined">
            <p className="hint" style={{ marginTop: 12 }}>
              <b>Declined by the agent</b> — criteria it judged no work in this repository
              could make true, each with the evaluated meaning of not doing it:
            </p>
            {acceptance.declined.map((declined, i) => (
              <div
                key={i}
                className="crit wait"
                data-testid="verify-declined-criterion"
              >
                <div className="c-mark">~</div>
                <div>
                  <div className="c-txt">{declined.text}</div>
                  <div className="c-proof" data-testid="verify-declined-reason">
                    {declined.reason}
                  </div>
                </div>
                <div className="c-stat">
                  {declined.spec ? `declined · ${declined.spec}` : "declined"}
                </div>
              </div>
            ))}
          </div>
        )}
        {report && <ReportLists acceptance={acceptance} />}
        {acceptance.ratifiedBy && (
          <p className="hint" style={{ marginTop: 12 }} data-testid="verify-ratified-by">
            ratified by {acceptance.ratifiedBy}
          </p>
        )}
      </div>
    </section>
  );
}

/** 2026-11-18-c8e2d: the rest of the masters' reports — what they fixed in passing and the follow-ups they propose. */
function ReportLists({ acceptance }: { acceptance: RunAcceptance }) {
  const fixed = acceptance.fixedInPassing ?? [];
  const followUps = acceptance.followUps ?? [];
  return (
    <>
      {fixed.length > 0 && (
        <div data-testid="verify-fixed-in-passing">
          <p className="hint" style={{ marginTop: 12 }}><b>Fixed in passing</b></p>
          {fixed.map((line, i) => (
            <div key={i} className="crit pass"><div className="c-mark">+</div><div><div className="c-txt">{line}</div></div><div className="c-stat">fixed</div></div>
          ))}
        </div>
      )}
      {followUps.length > 0 && (
        <div data-testid="verify-follow-ups">
          <p className="hint" style={{ marginTop: 12 }}>
            <b>Proposed follow-ups</b> — work a master judged not worth holding its spec for; file one from the ticket page.
          </p>
          {followUps.map((f, i) => (
            <div key={i} className="crit wait" data-testid="verify-follow-up">
              <div className="c-mark">→</div>
              <div><div className="c-txt">{f.goal}</div>{f.why && <div className="c-proof">{f.why}</div>}</div>
              <div className="c-stat">{f.specId ? `follow-up · ${f.specId}` : "follow-up"}</div>
            </div>
          ))}
        </div>
      )}
    </>
  );
}

// --- Legacy: the p0328 ExpectationRatified event view -----------------------

function fallbackBadgeClass(view: VerifyFallbackView): string {
  if (view.tone === "green") return "ok";
  if (view.tone === "rose") return "bad";
  return "neu";
}

function FallbackVerify({ view }: { view: VerifyFallbackView }) {
  const cls = view.ratified ? "pass" : view.tone === "rose" ? "fail" : "wait";
  return (
    <section className="card" data-testid="verify-summary" data-source="event-fallback">
      <div className="card-h">
        <h3>Verify — the ratified contract</h3>
        <span className={cn("badge", fallbackBadgeClass(view))} data-testid="verify-outcome-badge">
          {OUTCOME_LABEL[view.outcome] ?? view.outcome}
          {view.outcome === "edited" && view.editDistance > 0 ? ` · Δ${view.editDistance}` : ""}
        </span>
      </div>
      <div className="card-b">
        {!view.expectation || view.expectation.expected.length === 0 ? (
          <p className="hint" data-testid="verify-empty">
            {view.outcome === "none"
              ? "No ratified acceptance contract on this run yet — nothing has been proven green."
              : "The expectation was not ratified as a contract; its criteria are not shown as proven."}
          </p>
        ) : (
          <div data-testid="verify-criteria">
            {view.expectation.expected.map((criterion, i) => (
              <div key={i} className={cn("crit", cls)} data-testid="verify-criterion">
                <div className="c-mark">{cls === "pass" ? CHECK : cls === "fail" ? CROSS : "?"}</div>
                <div>
                  <div className="c-txt">{criterion}</div>
                </div>
                <div className="c-stat">{view.ratified ? "ratified" : view.outcome}</div>
              </div>
            ))}
          </div>
        )}
        <p className="hint" style={{ marginTop: 12 }}>
          This run predates persisted per-criterion dispositions — criteria reflect the ratified
          contract, not keystone proof against the diff.
        </p>
      </div>
    </section>
  );
}
