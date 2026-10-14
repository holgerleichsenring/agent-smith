"use client";

import type { SpecDialogReviewRound } from "@/types/spec-dialog";

// 2026-10-13-8762i: the review rounds a design turn's proposal went through before it reached the person who
// approves it — per draft, round by round, what each reviewer answered, the cuts the design partner applied,
// the findings, and any cut wording still standing. Drafts not reviewed yet say so; the reserved "series"
// entry is the series contract. A proposal stored before the rounds shipped carries neither field, and an
// absent field renders nothing.

export const SERIES_TOKEN = "series";

export function notReviewedLabel(entry: string): string {
  return entry === SERIES_TOKEN ? "the series contract is not reviewed yet" : `${entry} — not reviewed yet`;
}

export function DialogProposalRounds({
  rounds,
  notReviewed,
}: {
  rounds?: SpecDialogReviewRound[];
  notReviewed?: string[] | null;
}) {
  const waiting = notReviewed ?? [];
  if (!rounds?.length && waiting.length === 0) return null;
  const specIds = [...new Set((rounds ?? []).map((round) => round.specId))];
  return (
    <div data-testid="dialog-proposal-rounds" className="mt-1.5 mb-1.5">
      {specIds.map((specId) => (
        <div key={specId} data-testid={`dialog-rounds-${specId}`} className="mb-1">
          <div className="fl">{specId === SERIES_TOKEN ? "the series contract" : specId} — review rounds</div>
          <ol className="ml-4 flex list-decimal flex-col gap-1">
            {(rounds ?? []).filter((round) => round.specId === specId).map((round) => (
              <Round key={round.round} round={round} />
            ))}
          </ol>
        </div>
      ))}
      {waiting.map((entry) => (
        <p key={entry} data-testid="dialog-not-reviewed" className="dsh-body font-semibold text-ink">
          {notReviewedLabel(entry)}
        </p>
      ))}
    </div>
  );
}

function Round({ round }: { round: SpecDialogReviewRound }) {
  const reviewers = round.reviewerVerdicts.map((verdict) => verdict ?? "not taken").join(", ");
  return (
    <li data-testid="dialog-review-round" className="dsh-body text-ink">
      <span className="font-semibold">round {round.round}: {round.verdict}</span>
      <span className="ml-1 dsh-label text-body">(reviewers: {reviewers})</span>
      <ul className="ml-4 list-disc">
        {round.cuts.map((cut, index) => (
          <li key={`cut-${index}`} data-testid="dialog-round-cut">
            cut {cut.register}: “{cut.quote}” → {cut.replacement ? `“${cut.replacement}”` : "removed"}
            <span className="ml-1 dsh-mono font-mono text-body">{cut.evidence}</span>
          </li>
        ))}
        {round.findings.map((finding, index) => (
          <li key={`finding-${index}`} data-testid="dialog-round-finding">
            {finding.text}
            <span className="ml-1 dsh-mono font-mono text-body">{finding.evidence}</span>
          </li>
        ))}
        {round.residues.map((residue, index) => (
          <li key={`residue-${index}`} data-testid="dialog-round-residue">
            still stands in {residue.register}: “{residue.quote}”
          </li>
        ))}
      </ul>
    </li>
  );
}
