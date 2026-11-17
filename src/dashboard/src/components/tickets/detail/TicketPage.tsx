"use client";

import Link from "next/link";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import "@/styles/ticket-thread.css";
import { isNeverRun } from "../ticketFormat";
import { TicketHeader } from "./TicketHeader";
import { LiveAttemptCancel, TicketMoreMenu } from "./TicketMoreMenu";
import { TicketNeverRun } from "./TicketNeverRun";
import { sectionOf } from "./ticketPageSections";
import { TicketSectionBar } from "./TicketSectionBar";
import { TicketThread } from "./TicketThread";
import { TicketWaitsFor } from "./TicketWaitsFor";
import { SpecDerivations } from "./SpecDerivations";
import { CAUSE_PARAM, SECTION_PARAM, TAB_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59q: the ticket detail under .mock-shell.mock-tickets like the list.
// 2026-10-14-dbcen: a ticket with no run on record has nothing derived, attempted or spent — no thread:
// its header, what it waits for, what waits for it and the filed line (ticket-graph.html, never-run-page).
// 2026-11-17-8d1ca: a ticket with a run has its section bar beside the main area, which shows ONE section — What
// happened (what it waits for, the thread, Your move and the composer) unless ?section= names another. The heading
// carries the section's name, Cancel for a live attempt in plain sight, and the ··· menu.

export function TicketPage() {
  const detail = useTicketDetail();

  if (!detail.data) {
    return (
      <div className="mock-shell mock-tickets" data-testid="ticket-page">
        <main className="main tk-page">
          <TicketUnread notFound={detail.notFound === true} failed={detail.failed || detail.refusal !== null} />
        </main>
      </div>
    );
  }
  if (detail.data.attempts.length === 0 && isNeverRun(detail.data.summary)) {
    return (
      <div className="mock-shell mock-tickets" data-testid="ticket-page">
        <main className="main tk-page">
          <TicketHeader detail={detail.data} />
          <TicketWaitsFor project={detail.project} />
          <TicketNeverRun summary={detail.data.summary} />
          <SpecDerivations />
        </main>
      </div>
    );
  }
  return <TicketSections detail={detail.data} project={detail.project} />;
}

function TicketSections({ detail, project }: { detail: TicketDetail; project: string }) {
  const address = useTicketTabGo();
  const section = sectionOf(address.param(SECTION_PARAM), address.param(TAB_PARAM));
  const select = (id: string) => address.go(id, { [CAUSE_PARAM]: null });
  return (
    <div className="mock-shell mock-tickets tk-shell" data-testid="ticket-page">
      <TicketSectionBar detail={detail} current={section} onSelect={select} />
      <main className="main tk-page" data-section={section.id}>
        <div className="tk-heading">
          <h1 data-testid="ticket-section-title">{section.label}</h1>
          <LiveAttemptCancel />
          <TicketMoreMenu summary={detail.summary} />
        </div>
        {section.Pane ? (
          <section className="tk-pane" data-testid={`ticket-pane-${section.id}`}>
            <section.Pane />
          </section>
        ) : (
          <>
            <TicketWaitsFor project={project} />
            <TicketThread />
          </>
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
