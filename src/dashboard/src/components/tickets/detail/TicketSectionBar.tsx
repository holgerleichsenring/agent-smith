"use client";

import Link from "next/link";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { useTicketConversation, useTicketDefaults, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import { ticketDot } from "../ticketDot";
import { ticketLabel } from "../ticketFormat";
import { pullRequestFact, stateWord, trackerFact } from "./sectionBarWords";
import { ticketSections, type TicketSection } from "./ticketPageSections";

// 2026-11-17-8d1ca: the ticket's own bar beside the icon-only app sidebar (TicketRail mock): back to the list, the
// ticket's key, title and state in its colour, the sections with the count each one holds, and at its foot the
// project, the tracker and its status, the branch and the pull requests. Text and hairlines only.

interface Props {
  detail: TicketDetail;
  current: TicketSection;
  onSelect: (id: string) => void;
}

export function TicketSectionBar({ detail, current, onSelect }: Props) {
  const reads = { series: useTicketSeries().data, defaults: useTicketDefaults().data, conversation: useTicketConversation().data ?? null };
  const { summary, branch } = detail;
  const state = stateWord(summary);
  const tracker = trackerFact(summary);
  return (
    <aside className="tk-bar" aria-label="This ticket" data-testid="ticket-section-bar">
      <Link className="tk-bar-back" href={TICKETS_PATH} data-testid="ticket-back">← Tickets</Link>
      <div className="tk-bar-id" data-testid="ticket-head">
        <span className="mono tk-bar-key">{ticketLabel(summary)}</span>
        <span className="tk-bar-title">{summary.title ?? summary.ticketId}</span>
        {state && <span className={`tk-bar-state ${ticketDot(summary)}`} data-testid="ticket-state-word">{state}</span>}
      </div>
      <nav className="tk-bar-sections" aria-label="Ticket sections">
        {ticketSections.map((section) => {
          const counter = section.counter?.(detail, reads) ?? null;
          const tone = counter?.hot ? " hot" : counter?.ok ? " ok" : "";
          return (
            <button key={section.id} type="button" className="tk-bar-sec" aria-current={section.id === current.id ? "page" : undefined}
              onClick={() => onSelect(section.id)} data-testid={`ticket-section-${section.id}`}>
              <span className="tk-bar-label">{section.label}</span>
              {counter && <span className={`tk-bar-count${tone}`}>{counter.text}</span>}
            </button>
          );
        })}
      </nav>
      <div className="tk-bar-facts" data-testid="ticket-facts">
        <span>{summary.project}</span>
        {tracker && <span data-testid="ticket-tracker-fact">{tracker}</span>}
        {branch && <span className="mono" data-testid="ticket-branch">{branch}</span>}
        <span data-testid="ticket-pr-fate">{pullRequestFact(summary, detail.attempts)}</span>
      </div>
    </aside>
  );
}
