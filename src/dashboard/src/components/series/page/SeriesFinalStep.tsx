"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";

// 2026-11-24-f9c5f: the series' last step — its branch into the default branch, one pull request per repo — after the tickets:
// what it waits for (the tickets, the check), then each repo's pull request with what the series said about it, and a
// person's review and merge under review: end (2026-11-25-9992i). A series without a branch has no such step.

export function SeriesFinalStep({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const branch = detail.integrationBranch;
  if (!branch) return null;
  const left = detail.tickets.filter((ticket) => !ticket.landed).length;
  const deliveries = detail.deliveries ?? [];
  const waits = left > 0 ? t("series.final.waitsTickets", { count: left, branch })
    : detail.state === "done" ? t("series.final.done")
    : deliveries.length === 0 ? t("series.final.opens")
    : detail.state === "delivering" ? ((detail.review ?? "end") === "end" ? t("series.final.yourMerge") : t("series.final.merging"))
    : t("series.final.waitsCheck");
  return (
    <>
      <div className="sr-list-group" data-testid="series-list-final">{t("series.final.title", { branch })}</div>
      <div className="sr-list-row quiet" data-testid="series-final-step">
        <span className="sr-list-bar" />
        <div className="sr-list-main">
          <div className="sr-list-sub" data-testid="series-final-waits">{waits}</div>
          {deliveries.map((delivery) => (
            <div key={delivery.repo} className="sr-list-title" data-testid={`series-final-${delivery.repo}`}>
              <span className="mono">{delivery.repo}</span>{" "}
              <a href={delivery.url} target="_blank" rel="noreferrer">#{delivery.number}</a>
              {delivery.said && <span className="sr-list-sub"> {delivery.said}</span>}
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
