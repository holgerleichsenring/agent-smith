"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import { seriesTicketBadges } from "./seriesCiState";

// 2026-11-23-0219: what the host says about a ticket's waiting pull requests — Draft, Build not started, CI running, CI red —
// drawn in the node menu and on the action list's row.

const TONE = { draft: "", ciRed: " bad", ciNotStarted: " need", ciRunning: " run" } as const;

export function SeriesTicketBadges({ ticket }: { ticket: SeriesRunTicket }) {
  const t = useT();
  const badges = seriesTicketBadges(ticket);
  if (badges.length === 0) return null;
  return (
    <span className="sr-badges" data-testid={`series-badges-${ticket.ticketId}`}>
      {badges.map((badge) => (
        <span key={badge} className={`sr-state${TONE[badge]}`} data-badge={badge}>{t(`series.badge.${badge}`)}</span>
      ))}
    </span>
  );
}
