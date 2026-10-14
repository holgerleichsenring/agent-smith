"use client";

import Link from "next/link";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import { TicketHeader } from "./TicketHeader";
import { TicketTabs } from "./TicketTabs";
import { TAB_PARAM, ticketTab } from "./ticketTabTable";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59q: the ticket detail — header, tab bar, the selected tab's pane — under
// .mock-shell.mock-tickets like the list. ?tab= is read here, under the route's Suspense boundary.

export function TicketPage() {
  const detail = useTicketDetail();
  const address = useTicketTabGo();
  const tab = ticketTab(address.param(TAB_PARAM));
  const select = (id: string) => address.go(id, { [CAUSE_PARAM]: null });

  return (
    <div className="mock-shell mock-tickets" data-testid="ticket-page">
      <main className="main">
        {detail.data ? (
          <>
            <TicketHeader detail={detail.data} />
            <TicketTabs detail={detail.data} selected={tab} onSelect={select} />
            <section className="tabpane" id={`tab-${tab.id}`} role="tabpanel" aria-labelledby={`t-${tab.id}`}>
              <tab.Pane />
            </section>
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
