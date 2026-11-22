"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import type { SpecDialogReviewRound } from "@/types/spec-dialog";

// 2026-10-13-8762i: the review a design turn's proposal went through before it reached the person who approves it —
// per draft, what each reviewer answered, the cuts it suggests, the findings, and any cut wording still standing.
// 2026-11-12-cd4ch: one advisory pass records one round per draft; nothing is applied, so a cut is a suggestion. A
// proposal stored while the review ran in rounds still shows each of them. Drafts not yet reviewed say so; the reserved "series"
// entry is the series contract. A proposal stored before the rounds shipped carries neither field, and an
// absent field renders nothing.

export const SERIES_TOKEN = "series";

// 2026-11-10-8e59: information, not a hold — nothing is built before a review ends clean on its exact text.
export function notReviewedLabel(t: T, entry: string): string {
  const name = entry === SERIES_TOKEN ? t("dialog.rounds.seriesContract") : entry;
  return t("dialog.rounds.notReviewedLabel", { name, notYet: t("dialog.rounds.notYetReviewed") });
}

export function DialogProposalRounds({
  rounds,
  notReviewed,
}: {
  rounds?: SpecDialogReviewRound[];
  notReviewed?: string[] | null;
}) {
  const t = useT();
  const waiting = notReviewed ?? [];
  if (!rounds?.length && waiting.length === 0) return null;
  const specIds = [...new Set((rounds ?? []).map((round) => round.specId))];
  return (
    <div data-testid="dialog-proposal-rounds" className="mt-1.5 mb-1.5">
      {specIds.map((specId) => (
        <div key={specId} data-testid={`dialog-rounds-${specId}`} className="mb-1">
          <div className="fl">{t("dialog.rounds.review", { name: specId === SERIES_TOKEN ? t("dialog.rounds.seriesContract") : specId })}</div>
          <ol className="ml-4 flex list-decimal flex-col gap-1">
            {(rounds ?? []).filter((round) => round.specId === specId).map((round) => (
              <Round key={round.round} round={round} />
            ))}
          </ol>
        </div>
      ))}
      {waiting.map((entry) => (
        <p key={entry} data-testid="dialog-not-reviewed" className="dsh-body font-semibold text-ink">
          {notReviewedLabel(t, entry)}
        </p>
      ))}
    </div>
  );
}

function Round({ round }: { round: SpecDialogReviewRound }) {
  const t = useT();
  const reviewers = round.reviewerVerdicts.map((verdict) => verdict ?? t("dialog.rounds.notTaken")).join(", ");
  return (
    <li data-testid="dialog-review-round" className="dsh-body text-ink">
      <span className="font-semibold">{t("dialog.rounds.round", { round: round.round, verdict: round.verdict })}</span>
      <span className="ml-1 dsh-label text-body">{t("dialog.rounds.reviewers", { list: reviewers })}</span>
      <ul className="ml-4 list-disc">
        {round.cuts.map((cut, index) => (
          <li key={`cut-${index}`} data-testid="dialog-round-cut">
            {t("dialog.rounds.cut", {
              register: cut.register,
              quote: cut.quote,
              replacement: cut.replacement ? t("common.quoted", { text: cut.replacement }) : t("dialog.rounds.removed"),
            })}
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
            {t("dialog.rounds.residue", { register: residue.register, quote: residue.quote })}
          </li>
        ))}
      </ul>
    </li>
  );
}
