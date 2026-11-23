"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useT } from "@/hooks/useT";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { seriesTicketState } from "./seriesTicketState";

// 2026-11-23-31dcd: every ticket of the series in the order it can happen (by depth, the series' order within one): key and title,
// its repos, its state. A row opens the ticket's page; the key is the link, so Enter and a middle click work too.

export function SeriesTicketsSection({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const router = useRouter();
  return (
    <>
      <p className="msub">{t("series.tickets.sub")}</p>
      <table className="sr-tickets" data-testid="series-tickets">
        <thead><tr><th>{t("series.tickets.ticket")}</th><th>{t("series.tickets.repo")}</th><th>{t("series.tickets.state")}</th></tr></thead>
        <tbody>
          {[...detail.tickets].sort((a, b) => a.depth - b.depth).map((ticket) => {
            const href = ticketPath(detail.project, ticket.ticketId);
            const state = seriesTicketState(ticket, t);
            return (
              <tr key={ticket.ticketId} onClick={() => router.push(href)} data-testid={`series-ticket-${ticket.ticketId}`}>
                <td><Link className="mono" href={href} onClick={(event) => event.stopPropagation()}>{ticket.key}</Link> {ticket.title}</td>
                <td>{ticket.repos.join(" + ") || "–"}</td>
                <td><span className={`sr-state ${state.tone}`}>{state.text}</span></td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </>
  );
}
