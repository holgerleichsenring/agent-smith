"use client";

import { SectionLabel } from "@/components/ui/SectionLabel";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { cn } from "@/lib/utils";
import type { VerifyView } from "./beatMapping";

// p0344: the Verify beat's surface — the run's ratified acceptance contract
// (p0328) with an honest disposition. Emerald ONLY when the human actually
// ratified (verbatim/edited); rejected is rose; unratified or absent is
// neutral. Never renders a criterion green that the keystone did not prove.

const OUTCOME_LABEL: Record<string, string> = {
  verbatim: "Ratified verbatim",
  edited: "Ratified with edits",
  rejected: "Rejected",
  unratified: "Unratified",
  none: "No ratified contract",
};

const TONE_TO_BADGE: Record<VerifyView["tone"], BadgeTone> = {
  green: "green",
  rose: "rose",
  neutral: "neutral",
};

export function VerifySummary({ view }: { view: VerifyView }) {
  const criterionDot = view.ratified
    ? "bg-emerald-500"
    : view.tone === "rose"
    ? "bg-rose-500"
    : "bg-stone-300";

  return (
    <div data-testid="verify-summary" className="card-content p-4">
      <div className="flex items-center justify-between gap-3">
        <SectionLabel>Verify — proven vs the diff</SectionLabel>
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
        Criteria reflect the ratified contract. Per-criterion keystone proof against the
        diff and ledger coverage warnings are not wired to the client yet.
      </p>
    </div>
  );
}
