"use client";

import Link from "next/link";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import "@/styles/ticket-thread.css";
import { isNeverRun } from "../ticketFormat";
import { TicketDetails } from "./TicketDetails";
import { TicketHeader } from "./TicketHeader";
import { TicketNeverRun } from "./TicketNeverRun";
import { TicketThread } from "./TicketThread";
import { TicketWaitsFor } from "./TicketWaitsFor";
import { SpecDerivations } from "./SpecDerivations";

// 2026-10-13-9a59q: the ticket detail under .mock-shell.mock-tickets like the list.
// 2026-10-14-dbcen: a ticket with no run on record has nothing derived, attempted or spent — no thread:
// its header, what it waits for, what waits for it and the filed line (ticket-graph.html, never-run-page).
// 2026-11-16-6877a: a ticket with a run is its conversation — header, what it waits for, the thread with the
// Details fold at its end, and the Your move box with the composer docked at the bottom.

export function TicketPage() {
  const detail = useTicketDetail();

  return (
    <div className="mock-shell mock-tickets" data-testid="ticket-page">
      <main className="main tk-page">
        {detail.data && detail.data.attempts.length === 0 && isNeverRun(detail.data.summary) ? (
          <>
            <TicketHeader detail={detail.data} />
            <TicketWaitsFor project={detail.project} />
            <TicketNeverRun summary={detail.data.summary} />
            <SpecDerivations />
          </>
        ) : detail.data ? (
          <>
            <TicketHeader detail={detail.data} />
            <TicketWaitsFor project={detail.project} />
            <TicketThread after={<TicketDetails detail={detail.data} />} />
          </>
        ) : (
          <TicketUnread notFound={detail.notFound === true} failed={detail.failed || detail.refusal !== null} />
        )}
      </main>
    </div>
  );
}

function TicketUnread({ notFound, failed }: { notFound: boolean; failed: boolean }) {
  if (!notFound && !failed) return <div className="msub" data-testid="ticket-loading">Reading the ticket…</div>;
  return (
    <div className="msub" data-testid="ticket-unread">
      {notFound ? "No attempt at this ticket is recorded." : "The ticket could not be read."}{" "}
      <Link href={TICKETS_PATH}>Back to the tickets</Link>
    </div>
  );
}
