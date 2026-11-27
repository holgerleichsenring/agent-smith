"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { namesOf } from "../../tickets/holds/holdWords";
import {
  SERIES_BUCKETS, SERIES_TICKET_FILTERS, seriesBucketCounts, seriesTicketBucket, seriesTicketRows, seriesWaitsOn,
  type SeriesTicketFilter,
} from "./seriesTicketRows";

// 2026-11-27-ecf2a: the series' tickets in its rail — a thin meter by state, the counts, Open / All / Merged, and one row per
// ticket: its dot, key, title and a short state. Pointing at a row lights the ticket's course in the graph (the page holds
// which); leaving the list puts the graph back. A click opens the ticket's details beside the row.

interface Props {
  detail: SeriesRunDetail;
  hovered: string | null;
  onHover: (ticketId: string | null) => void;
  onOpen: (ticketId: string, row: HTMLElement) => void;
}

const FILTER_WORDS: Record<SeriesTicketFilter, TextKey> = {
  open: "series.rail.open", all: "series.rail.all", merged: "series.rail.mergedOnly",
};

export function SeriesTicketList({ detail, hovered, onHover, onOpen }: Props) {
  const t = useT();
  const [filter, setFilter] = useState<SeriesTicketFilter>("all");
  const counts = seriesBucketCounts(detail.tickets);
  const total = detail.tickets.length;
  const rows = seriesTicketRows(detail.tickets, filter);
  const share = (n: number) => `${total === 0 ? 0 : (100 * n) / total}%`;
  const said = [t("series.rail.merged", { merged: counts.merged, total }),
    counts.you > 0 ? t("series.rail.you", { count: counts.you }) : null,
    counts.waiting > 0 ? t("series.rail.waiting", { count: counts.waiting }) : null].filter(Boolean).join(" · ");
  const stateOf = (ticket: SeriesRunTicket): string | null => {
    if (ticket.kind === "merged") return null;
    const waits = seriesWaitsOn(ticket, detail.tickets);
    return waits.length > 0 ? t("series.rail.waitsOn", { keys: namesOf(t, waits) }) : t(`series.kind.${ticket.kind}`);
  };
  return (
    <div className="sr-rail-list" data-testid="series-ticket-list">
      <div className="sr-bar sr-meter" aria-hidden="true" data-testid="series-meter">
        {SERIES_BUCKETS.map((bucket) => counts[bucket] > 0 &&
          <span key={bucket} className={`sr-m-${bucket}`} style={{ width: share(counts[bucket]) }} data-testid={`series-meter-${bucket}`} />)}
      </div>
      <div className="sr-rail-counts" data-testid="series-rail-counts">{said}</div>
      <div className="sr-filters" role="group" aria-label={t("series.rail.filter")}>
        {SERIES_TICKET_FILTERS.map((f) => (
          <button key={f} type="button" className="sr-filter-btn" aria-pressed={filter === f} onClick={() => setFilter(f)}
            data-testid={`series-filter-${f}`}>{t(FILTER_WORDS[f])}</button>
        ))}
      </div>
      <ul className="sr-trows" aria-label={t("series.rail.tickets")} title={t("series.rail.hint")}
        onMouseLeave={() => onHover(null)} onBlur={(e) => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) onHover(null); }}>
        {rows.length === 0 && <li className="sr-trows-none">{t("series.rail.none")}</li>}
        {rows.map((ticket) => {
          const state = stateOf(ticket);
          return (
            <li key={ticket.ticketId}>
              <button type="button" className={`sr-trow${hovered === ticket.ticketId ? " on" : ""}`} data-testid={`series-row-${ticket.ticketId}`}
                onMouseEnter={() => onHover(ticket.ticketId)} onFocus={() => onHover(ticket.ticketId)}
                onClick={(e) => onOpen(ticket.ticketId, e.currentTarget)} aria-haspopup="dialog">
                <span className={`sr-tdot ${seriesTicketBucket(ticket.kind)}`} aria-hidden="true" />
                <span className="sr-tkey">{ticket.key}</span>
                <span className="sr-ttitle">
                  <span className="sr-tname">{ticket.title}</span>
                  {state && <small className="sr-tstate" data-testid={`series-row-state-${ticket.ticketId}`}>{state}</small>}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
