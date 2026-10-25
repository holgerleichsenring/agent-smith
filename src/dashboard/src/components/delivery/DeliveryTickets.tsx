"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { DeliveryTicket } from "@/lib/deliveryApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { formatAt, formatDuration, formatUsd, formatWork } from "@/lib/delivery/deliveryFormat";
import { bucketLabel, bucketTone } from "./bucketPill";

// 2026-10-25-0099c: the period's tickets, latest activity first; a row opens the ticket's page.

export function DeliveryTickets({ tickets }: { tickets: DeliveryTicket[] }) {
  const router = useRouter();
  return (
    <section className="panel" aria-labelledby="delivery-tk" data-testid="delivery-tickets">
      <div className="panel-head">
        <h2 id="delivery-tk">Tickets</h2>
        <span className="sub">Latest activity first. A row opens the ticket.</span>
      </div>
      <div className="tbl-scroll">
        <table>
          <thead>
            <tr>
              <th>Ticket</th><th>Project</th><th>Outcome</th><th>What was done</th><th className="num">Attempts</th>
              <th className="num">Specs</th><th className="num">Agent time</th><th className="num">Wall time</th>
              <th className="num">Cost</th><th>Last activity</th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((t) => (
              <tr key={`${t.project}/${t.ticketId}`} className="row" data-testid={`delivery-ticket-${t.ticketId}`}
                onClick={() => router.push(ticketPath(t.project, t.ticketId))}>
                <td><Link href={ticketPath(t.project, t.ticketId)} onClick={(e) => e.stopPropagation()}><b>{t.ticketId}</b> {t.title}</Link></td>
                <td className="mono">{t.project}</td>
                <td><span className={`pill ${bucketTone(t.bucket)}`}>{bucketLabel(t.bucket)}</span></td>
                <td className="did">{formatWork(t.specTitles, t.repos)}</td>
                <td className="num">{t.attempts.length}</td>
                <td className="num">{t.specsDone}/{t.specsTotal}</td>
                <td className="num">{formatDuration(t.agentSeconds)}</td>
                <td className="num">{formatDuration(t.wallSeconds)}</td>
                <td className="num">{formatUsd(t.costUsd)}</td>
                <td className="mono nowrap">{formatAt(t.lastActivityAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
