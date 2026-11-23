"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { AcceptanceCriterion, RunAcceptance } from "@/types/hub-events";
import type { VerifyFallbackView } from "./verifyFallback";
import { CriterionJudgementControl } from "./CriterionJudgementControl";
import { UnmeasuredAccounts } from "./UnmeasuredAccounts";
import { MasterReport } from "@/components/jobs/runlog/LogText";
import { reportCount, unmeasuredWords } from "@/components/jobs/runlog/runSections";
import type { CriterionDisposition, CriterionJudgement } from "@/lib/judgementsApi";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";

// p0344b: the Verify beat's surface. Preferred source is the run's PERSISTED
// per-criterion acceptance dispositions (snapshot.acceptance — the p0340
// keystone verdicts). Runs persisted before that field fall back to the p0328
// ExpectationRatified event view — and a run with neither renders an honest
// "nothing ratified" empty state. Never a fabricated green.
// p0343c (pixel identity): emits the run-viewer.html verify DOM verbatim —
// a .card with the "Verify against the ratified acceptance" header and .crit
// rows (pass/fail/wait) with c-mark, c-txt, c-proof and c-stat.

const OUTCOME_LABEL: Record<string, TextKey> = {
  verbatim: "runs.story.outcome.verbatim",
  edited: "runs.story.outcome.edited",
  rejected: "runs.story.outcome.rejected",
  unratified: "runs.story.outcome.unratified",
  none: "runs.story.outcome.none",
};

/** An outcome in words; one this map does not carry is shown as the server wrote it. */
function outcomeLabel(t: T, outcome: string): string | undefined {
  const key = OUTCOME_LABEL[outcome];
  return key ? t(key) : undefined;
}

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

const CRIT_STAT: Record<AcceptanceCriterion["status"], TextKey> = {
  met: "runs.story.stat.met",
  unmet: "runs.story.stat.unmet",
  not_applicable: "runs.story.stat.not_applicable",
  unproven: "runs.story.stat.unproven",
};

// 2026-10-12-6bfad: an unmeasured account is never one of the N; it makes the badge bad
// and says so after the count, or alone when there is nothing to count.
function badgeFor(t: T, acceptance: RunAcceptance): { cls: string; label: string } {
  const unmeasured = acceptance.unmeasured?.length ?? 0;
  // 2026-11-22-7103a: a run whose masters wrote reports judged no line — the badge counts the reports, never "reported".
  if (acceptance.reports != null && acceptance.criteria.length === 0) {
    const count = reportCount(t, acceptance);
    return unmeasured > 0 ? { cls: "bad", label: `${count} · ${unmeasuredWords(t, unmeasured)}` } : { cls: "", label: count };
  }
  const counted = criteriaBadge(t, acceptance);
  if (unmeasured === 0) return counted;
  return {
    cls: "bad",
    label: acceptance.criteria.length > 0 ? `${counted.label} · ${unmeasuredWords(t, unmeasured)}` : unmeasuredWords(t, unmeasured),
  };
}

function criteriaBadge(t: T, acceptance: RunAcceptance): { cls: string; label: string } {
  const total = acceptance.criteria.length;
  if (total === 0) return { cls: "neu", label: outcomeLabel(t, acceptance.outcome ?? "none") ?? t("runs.story.badge.noCriteria") };
  const met = acceptance.criteria.filter((c) => c.status === "met").length;
  const unmet = acceptance.criteria.filter((c) => c.status === "unmet").length;
  if (unmet > 0) return { cls: "bad", label: t("runs.story.badge.failed", { met, total, unmet }) };
  if (met === total) return { cls: "ok", label: t("runs.story.badge.proven", { met, total }) };
  return { cls: "neu", label: t("runs.story.badge.proven", { met, total }) };
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
  const t = useT();
  const badge = badgeFor(t, acceptance);
  const unmeasured = acceptance.unmeasured ?? [];
  // 2026-11-18-c8e2d: a run whose rows are its masters' reports carries the report's lists (maybe empty).
  const report = acceptance.fixedInPassing != null || acceptance.followUps != null || acceptance.reports != null;
  // 2026-11-18-98fa: the masters' own markdown reports, rendered as written — no row is judged on such a run.
  const reports = acceptance.reports ?? [];
  return (
    <section className="card" data-testid="verify-summary" data-source="acceptance">
      <div className="card-h">
        <h3>{report ? t("runs.story.title.report") : t("runs.story.title.verify")}</h3>
        <span className={cn("badge", badge.cls)} data-testid="verify-outcome-badge">
          {badge.label}
        </span>
      </div>
      <div className="card-b">
        <div className="hint" style={{ marginBottom: 6 }} data-testid="verify-source">
          {reports.length > 0 || (acceptance.reports != null && acceptance.criteria.length === 0) ? (
            <>
              {t("runs.story.source.reports.a")}<b>{t("runs.story.source.reports.b")}</b>{t("runs.story.source.reports.c")}
            </>
          ) : report ? (
            <>
              {t("runs.story.source.report.a")}<b>{t("runs.story.source.report.b")}</b>{t("runs.story.source.report.c")}
            </>
          ) : acceptance.source === "master_verification" ? (
            <>
              {t("runs.story.source.master.a")}<b>{t("runs.story.source.master.b")}</b>{t("runs.story.source.master.c")}
            </>
          ) : (
            <>
              {t("runs.story.source.gate.a")}<b>{t("runs.story.source.gate.b")}</b>{t("runs.story.source.gate.c")}
            </>
          )}
        </div>
        {unmeasured.length > 0 && <UnmeasuredAccounts accounts={unmeasured} />}
        {reports.map((r, i) => (
          <div key={i} data-testid="verify-report" style={{ marginTop: i > 0 ? 12 : 0 }}>
            <MasterReport text={r.markdown} spec={r.specId ? t("runs.story.spec", { spec: r.specId }) : null} testId="verify-report-text" folded={reports.length > 1} />
          </div>
        ))}
        {acceptance.criteria.length === 0 ? (
          unmeasured.length === 0 && acceptance.reports == null && (
            <p className="hint" data-testid="verify-empty">
              {t("runs.story.empty")}
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
                        {t("runs.story.cited")}{renderCitation ? renderCitation(criterion.citation) : criterion.citation}
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
                  <div className="c-stat">{t(CRIT_STAT[criterion.status])}</div>
                </div>
              );
            })}
          </div>
        )}
        {acceptance.declined && acceptance.declined.length > 0 && (
          <div data-testid="verify-declined">
            <p className="hint" style={{ marginTop: 12 }}>
              <b>{t("runs.story.declined.head")}</b>{t("runs.story.declined.sub")}
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
                  {declined.spec ? t("runs.story.declined.spec", { spec: declined.spec }) : t("runs.story.declined")}
                </div>
              </div>
            ))}
          </div>
        )}
        {report && <ReportLists acceptance={acceptance} />}
        {acceptance.ratifiedBy && (
          <p className="hint" style={{ marginTop: 12 }} data-testid="verify-ratified-by">
            {t("runs.story.ratifiedBy", { who: acceptance.ratifiedBy })}
          </p>
        )}
      </div>
    </section>
  );
}

/** 2026-11-18-c8e2d: the rest of the masters' reports — what they fixed in passing and the follow-ups they propose. */
function ReportLists({ acceptance }: { acceptance: RunAcceptance }) {
  const t = useT();
  const fixed = acceptance.fixedInPassing ?? [];
  const followUps = acceptance.followUps ?? [];
  return (
    <>
      {fixed.length > 0 && (
        <div data-testid="verify-fixed-in-passing">
          <p className="hint" style={{ marginTop: 12 }}><b>{t("runs.story.fixedInPassing")}</b></p>
          {fixed.map((line, i) => (
            <div key={i} className="crit pass"><div className="c-mark">+</div><div><div className="c-txt">{line}</div></div><div className="c-stat">{t("runs.story.fixed")}</div></div>
          ))}
        </div>
      )}
      {followUps.length > 0 && (
        <div data-testid="verify-follow-ups">
          <p className="hint" style={{ marginTop: 12 }}>
            <b>{t("runs.story.followUps.head")}</b>{t("runs.story.followUps.sub")}
          </p>
          {followUps.map((f, i) => (
            <div key={i} className="crit wait" data-testid="verify-follow-up">
              <div className="c-mark">→</div>
              <div><div className="c-txt">{f.goal}</div>{f.why && <div className="c-proof">{f.why}</div>}</div>
              <div className="c-stat">{f.specId ? t("runs.story.followUp.spec", { spec: f.specId }) : t("runs.story.followUp")}</div>
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
  const t = useT();
  const cls = view.ratified ? "pass" : view.tone === "rose" ? "fail" : "wait";
  return (
    <section className="card" data-testid="verify-summary" data-source="event-fallback">
      <div className="card-h">
        <h3>{t("runs.story.fallback.title")}</h3>
        <span className={cn("badge", fallbackBadgeClass(view))} data-testid="verify-outcome-badge">
          {outcomeLabel(t, view.outcome) ?? view.outcome}
          {view.outcome === "edited" && view.editDistance > 0 ? ` · Δ${view.editDistance}` : ""}
        </span>
      </div>
      <div className="card-b">
        {!view.expectation || view.expectation.expected.length === 0 ? (
          <p className="hint" data-testid="verify-empty">
            {view.outcome === "none"
              ? t("runs.story.fallback.none")
              : t("runs.story.fallback.notRatified")}
          </p>
        ) : (
          <div data-testid="verify-criteria">
            {view.expectation.expected.map((criterion, i) => (
              <div key={i} className={cn("crit", cls)} data-testid="verify-criterion">
                <div className="c-mark">{cls === "pass" ? CHECK : cls === "fail" ? CROSS : "?"}</div>
                <div>
                  <div className="c-txt">{criterion}</div>
                </div>
                <div className="c-stat">{view.ratified ? t("runs.story.fallback.ratified") : view.outcome}</div>
              </div>
            ))}
          </div>
        )}
        <p className="hint" style={{ marginTop: 12 }}>
          {t("runs.story.fallback.legacy")}
        </p>
      </div>
    </section>
  );
}
