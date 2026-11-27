"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { seriesRepoChipLabels } from "./seriesRepoLabels";

// 2026-11-27-ecf2b: one chip per pull request of a ticket, on its graph node and its row in the rail — the repo's short
// label, a link to the pull request in a new tab, filled when the pull request carries code and outlined when it carries
// only the spec record (spec, done index, decisions).

interface Props {
  ticket: SeriesRunTicket;
  labels: Record<string, string>;
  /** Drawn as plain text, holding a node's room for the chips over it. */
  still?: boolean;
  /** Where they stand — a graph node or a row of the rail — which names their test ids. */
  where?: "node" | "row";
}

/** One short label per repo of the series, from its repos and every pull request's. */
export function seriesChipLabels(detail: SeriesRunDetail): Record<string, string> {
  return seriesRepoChipLabels([...detail.repos, ...detail.tickets.flatMap((t) => t.pullRequests.map((p) => p.repo))]);
}

export function SeriesRepoChips({ ticket, labels, still = false, where = "node" }: Props) {
  const t = useT();
  if (ticket.pullRequests.length === 0) return null;
  if (still) {
    return <span className="sr-chips">{ticket.pullRequests.map((pr) => <span key={pr.url} className="sr-chip">{labels[pr.repo] ?? pr.repo.charAt(0).toUpperCase()}</span>)}</span>;
  }
  return (
    <span className="sr-chips">
      {ticket.pullRequests.map((pr) => {
        const what = pr.recordOnly ? t("series.chip.record") : t("series.chip.code");
        const title = t("series.chip.title", { repo: pr.repo, number: pr.number, what });
        return (
          <a key={pr.url} href={pr.url} target="_blank" rel="noreferrer" className={`sr-chip${pr.recordOnly ? "" : " code"}`}
            title={title} aria-label={title} onClick={(e) => e.stopPropagation()}
            data-testid={`series-${where}-chip-${pr.repo}-${ticket.ticketId}`} data-record-only={pr.recordOnly ? "true" : undefined}>
            {labels[pr.repo] ?? pr.repo.charAt(0).toUpperCase()}
          </a>
        );
      })}
    </span>
  );
}
