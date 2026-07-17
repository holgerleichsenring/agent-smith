"use client";

import { SectionLabel } from "@/components/ui/SectionLabel";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { cn } from "@/lib/utils";
import type { AcceptanceCriterion, RunAcceptance } from "@/types/hub-events";
import type { VerifyFallbackView } from "./verifyFallback";

// p0344b: the Verify beat's surface. Preferred source is the run's PERSISTED
// per-criterion acceptance dispositions (snapshot.acceptance — the p0340
// keystone verdicts): met=emerald, unmet=rose, not_applicable=neutral+reason,
// unproven=amber+reason. Runs persisted before that field exist fall back to
// the p0328 ExpectationRatified event view — and a run with neither renders an
// honest "nothing ratified" empty state. Never a fabricated green.

const OUTCOME_LABEL: Record<string, string> = {
  verbatim: "Ratified verbatim",
  edited: "Ratified with edits",
  rejected: "Rejected",
  unratified: "Unratified",
  none: "No ratified contract",
};

const TONE_TO_BADGE: Record<VerifyFallbackView["tone"], BadgeTone> = {
  green: "green",
  rose: "rose",
  neutral: "neutral",
};

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

const CRITERION_DOT: Record<AcceptanceCriterion["status"], string> = {
  met: "bg-emerald-500",
  unmet: "bg-rose-500",
  not_applicable: "bg-stone-300",
  unproven: "bg-amber-500",
};

const CRITERION_CAPTION: Record<AcceptanceCriterion["status"], string> = {
  met: "met",
  unmet: "unmet",
  not_applicable: "n/a",
  unproven: "unproven",
};

const CRITERION_CAPTION_TONE: Record<AcceptanceCriterion["status"], string> = {
  met: "text-emerald-600",
  unmet: "text-rose-600",
  not_applicable: "text-stone-400",
  unproven: "text-amber-600",
};

function outcomeTone(outcome: string | null): BadgeTone {
  if (outcome === "verbatim" || outcome === "edited") return "green";
  if (outcome === "rejected") return "rose";
  return "neutral";
}

function AcceptanceVerify({ acceptance }: { acceptance: RunAcceptance }) {
  const outcome = acceptance.outcome;
  return (
    <div data-testid="verify-summary" data-source="acceptance" className="card-content p-4">
      <div className="flex items-center justify-between gap-3">
        <SectionLabel>Verify — proven vs the diff</SectionLabel>
        <Badge tone={outcomeTone(outcome)} testId="verify-outcome-badge">
          {outcome ? OUTCOME_LABEL[outcome] ?? outcome : "No outcome recorded"}
        </Badge>
      </div>

      {acceptance.criteria.length === 0 ? (
        <p data-testid="verify-empty" className="mt-2 dsh-body text-stone-500">
          No acceptance criteria were recorded on this run — nothing has been proven green.
        </p>
      ) : (
        <ul className="mt-3 space-y-2" data-testid="verify-criteria">
          {acceptance.criteria.map((criterion, i) => (
            <li
              key={i}
              data-testid="verify-criterion"
              data-status={criterion.status}
              className="flex items-start gap-2.5"
            >
              <span
                className={cn(
                  "mt-1.5 h-2 w-2 flex-none rounded-full",
                  CRITERION_DOT[criterion.status],
                )}
                aria-hidden="true"
              />
              <span className="min-w-0">
                <span
                  className={cn(
                    "dsh-body",
                    criterion.status === "not_applicable" ? "text-stone-500" : "text-stone-800",
                  )}
                >
                  {criterion.text}
                  <span
                    className={cn(
                      "ml-2 dsh-label font-medium",
                      CRITERION_CAPTION_TONE[criterion.status],
                    )}
                  >
                    {CRITERION_CAPTION[criterion.status]}
                  </span>
                </span>
                {criterion.reason && (
                  <span
                    data-testid="verify-criterion-reason"
                    className="block dsh-label text-stone-500"
                  >
                    {criterion.reason}
                  </span>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {acceptance.ratifiedBy && (
        <p data-testid="verify-ratified-by" className="mt-3 dsh-label text-stone-400">
          ratified by {acceptance.ratifiedBy}
        </p>
      )}
    </div>
  );
}

// --- Legacy: the p0328 ExpectationRatified event view -----------------------

function FallbackVerify({ view }: { view: VerifyFallbackView }) {
  const criterionDot = view.ratified
    ? "bg-emerald-500"
    : view.tone === "rose"
    ? "bg-rose-500"
    : "bg-stone-300";

  return (
    <div data-testid="verify-summary" data-source="event-fallback" className="card-content p-4">
      <div className="flex items-center justify-between gap-3">
        <SectionLabel>Verify — the ratified contract</SectionLabel>
        <Badge tone={TONE_TO_BADGE[view.tone]} testId="verify-outcome-badge">
          {OUTCOME_LABEL[view.outcome] ?? view.outcome}
          {view.outcome === "edited" && view.editDistance > 0 ? ` · Δ${view.editDistance}` : ""}
        </Badge>
      </div>

      {!view.expectation || view.expectation.expected.length === 0 ? (
        <p data-testid="verify-empty" className="mt-2 dsh-body text-stone-500">
          {view.outcome === "none"
            ? "No ratified acceptance contract on this run yet — nothing has been proven green."
            : "The expectation was not ratified as a contract; its criteria are not shown as proven."}
        </p>
      ) : (
        <ul className="mt-3 space-y-2" data-testid="verify-criteria">
          {view.expectation.expected.map((criterion, i) => (
            <li
              key={i}
              data-testid="verify-criterion"
              className="flex items-start gap-2.5"
            >
              <span
                className={cn("mt-1.5 h-2 w-2 flex-none rounded-full", criterionDot)}
                aria-hidden="true"
              />
              <span
                className={cn(
                  "dsh-body",
                  view.ratified ? "text-stone-800" : "text-stone-500",
                )}
              >
                {criterion}
              </span>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-3 dsh-label text-stone-400">
        This run predates persisted per-criterion dispositions — criteria reflect the
        ratified contract, not keystone proof against the diff.
      </p>
    </div>
  );
}
