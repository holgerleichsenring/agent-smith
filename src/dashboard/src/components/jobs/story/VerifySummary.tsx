"use client";

import { cn } from "@/lib/utils";
import type { AcceptanceCriterion, RunAcceptance } from "@/types/hub-events";
import type { VerifyFallbackView } from "./verifyFallback";

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
  /** Event-derived legacy view — used ONLY when acceptance is absent. */
  fallback: VerifyFallbackView;
}

export function VerifySummary({ acceptance, fallback }: VerifySummaryProps) {
  return acceptance ? (
    <AcceptanceVerify acceptance={acceptance} />
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

function badgeFor(acceptance: RunAcceptance): { cls: string; label: string } {
  const total = acceptance.criteria.length;
  if (total === 0) return { cls: "neu", label: OUTCOME_LABEL[acceptance.outcome ?? "none"] ?? "no criteria" };
  const met = acceptance.criteria.filter((c) => c.status === "met").length;
  const unmet = acceptance.criteria.filter((c) => c.status === "unmet").length;
  if (unmet > 0) return { cls: "bad", label: `${met} of ${total} · ${unmet} failed` };
  if (met === total) return { cls: "ok", label: `${met} of ${total} proven` };
  return { cls: "neu", label: `${met} of ${total} proven` };
}

function AcceptanceVerify({ acceptance }: { acceptance: RunAcceptance }) {
  const badge = badgeFor(acceptance);
  return (
    <section className="card" data-testid="verify-summary" data-source="acceptance">
      <div className="card-h">
        <h3>Verify against the ratified acceptance</h3>
        <span className={cn("badge", badge.cls)} data-testid="verify-outcome-badge">
          {badge.label}
        </span>
      </div>
      <div className="card-b">
        <div className="hint" style={{ marginBottom: 6 }}>
          Each criterion the requester agreed to — checked against the <b>real diff and test
          run</b>, not the agent’s say-so.
        </div>
        {acceptance.criteria.length === 0 ? (
          <p className="hint" data-testid="verify-empty">
            No acceptance criteria were recorded on this run — nothing has been proven green.
          </p>
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
                    <div className="c-txt">{criterion.text}</div>
                    {criterion.reason && (
                      <div className="c-proof" data-testid="verify-criterion-reason">
                        {criterion.reason}
                      </div>
                    )}
                  </div>
                  <div className="c-stat">{CRIT_STAT[criterion.status]}</div>
                </div>
              );
            })}
          </div>
        )}
        {acceptance.ratifiedBy && (
          <p className="hint" style={{ marginTop: 12 }} data-testid="verify-ratified-by">
            ratified by {acceptance.ratifiedBy}
          </p>
        )}
      </div>
    </section>
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
