"use client";

import { useDateLocale, useT } from "@/hooks/useT";
import { useTicketReviews } from "@/lib/tickets/TicketPageStore";
import type { TicketReviewRound } from "@/types/tickets";
import { SERIES_TOKEN } from "@/components/dialog/DialogProposalRounds";
import { clock } from "./flowWords";
import { CutLine, FindingLine, roundGroups, roundLists, VerdictPill } from "./ReviewRounds";

// 2026-10-13-8762l: the series contract review — a round over the set, not one spec — between the spec grid
// and the revisions: each run's series rounds and the design conversation's series round, with the verdict,
// the findings and the cuts naming the member they edit; a member is a button that opens that spec. Shown
// only when a series round exists. Composed from the mock's .section-head/.blk/.crit/.ci/.pill classes.

export function SeriesContractReview({ onSelect }: { onSelect: (specId: string) => void }) {
  const t = useT();
  const locale = useDateLocale();
  const groups = roundGroups(t, locale, useTicketReviews().data, (r) => r.scope === "series", (r) => r.specId === SERIES_TOKEN);
  if (groups.length === 0) return null;
  const count = groups.reduce((n, g) => n + g.rounds.length, 0);
  return (
    <>
      <div className="section-head">
        <h2>{t("tickets.review.seriesTitle")}</h2><span className="cnt">{count}</span>
        <span className="sh-sub">{t("tickets.review.seriesSub")}</span>
      </div>
      <div className="blk" data-testid="series-review">
        {groups.map((group) => (
          <div key={group.label} className="crit" data-testid="series-review-group">
            <div className="dim-line">{group.label}</div>
            {group.rounds.map((round) => <SeriesRound key={round.round} round={round} onSelect={onSelect} />)}
          </div>
        ))}
      </div>
    </>
  );
}

function SeriesRound({ round, onSelect }: { round: TicketReviewRound; onSelect: (specId: string) => void }) {
  const t = useT();
  const locale = useDateLocale();
  const { cuts, findings } = roundLists(round);
  return (
    <div className="ci" data-testid="series-round">
      <span className="m q">{round.round}</span>
      <span>
        <VerdictPill verdict={round.verdict} />
        {findings.map((finding, i) => <FindingLine key={`f${i}`} finding={finding} />)}
        {cuts.map((cut, i) => (
          <CutLine key={`c${i}`} cut={cut} member={cut.specId && (
            <button type="button" className="btn sm" onClick={() => onSelect(cut.specId!)}
              data-testid={`series-member-${cut.specId}`}>{cut.specId}</button>
          )} />
        ))}
      </span>
      <span className="cw">{round.revisionNumber != null ? t("tickets.specs.rev", { n: round.revisionNumber }) : clock(round.at, locale) ?? ""}</span>
    </div>
  );
}
