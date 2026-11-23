"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { DeliveryTicket } from "@/lib/deliveryApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { useLanguage } from "@/hooks/useT";
import { formatAt, formatDuration, formatUsd, formatWork, wording } from "@/lib/delivery/deliveryFormat";
import { bucketLabel, bucketTone } from "./bucketPill";

// 2026-10-25-0099c: the period's tickets, latest activity first; a row opens the ticket's page.

export function DeliveryTickets({ tickets }: { tickets: DeliveryTicket[] }) {
  const router = useRouter();
  const w = wording(useLanguage());
  const { t } = w;
  return (
    <section className="panel" aria-labelledby="delivery-tk" data-testid="delivery-tickets">
      <div className="panel-head">
        <h2 id="delivery-tk">{t("delivery.col.tickets")}</h2>
        <span className="sub">{t("delivery.tickets.sub")}</span>
      </div>
      <div className="tbl-scroll">
        <table>
          <thead>
            <tr>
              <th>{t("delivery.col.ticket")}</th><th>{t("delivery.col.project")}</th><th>{t("delivery.col.outcome")}</th>
              <th>{t("delivery.col.work")}</th><th className="num">{t("delivery.col.attempts")}</th>
              <th className="num">{t("delivery.col.specs")}</th><th className="num">{t("delivery.col.agentTime")}</th>
              <th className="num">{t("delivery.col.wallTime")}</th><th className="num">{t("delivery.col.cost")}</th>
              <th>{t("delivery.col.lastActivity")}</th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((ticket) => (
              <tr key={`${ticket.project}/${ticket.ticketId}`} className="row" data-testid={`delivery-ticket-${ticket.ticketId}`}
                onClick={() => router.push(ticketPath(ticket.project, ticket.ticketId))}>
                <td><Link href={ticketPath(ticket.project, ticket.ticketId)} onClick={(e) => e.stopPropagation()}><b>{ticket.ticketId}</b> {ticket.title}</Link></td>
                <td className="mono">{ticket.project}</td>
                <td><span className={`pill ${bucketTone(ticket.bucket)}`}>{bucketLabel(ticket.bucket)}</span></td>
                <td className="did">{formatWork(t, ticket.specTitles, ticket.repos)}</td>
                <td className="num">{ticket.attempts.length}</td>
                <td className="num">{ticket.specsDone}/{ticket.specsTotal}</td>
                <td className="num">{formatDuration(w, ticket.agentSeconds)}</td>
                <td className="num">{formatDuration(w, ticket.wallSeconds)}</td>
                <td className="num">{formatUsd(w, ticket.costUsd)}</td>
                <td className="mono nowrap">{formatAt(ticket.lastActivityAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
