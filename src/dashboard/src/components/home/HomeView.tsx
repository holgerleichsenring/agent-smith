"use client";

import Link from "next/link";
import { ticketHref } from "@/components/tickets/TicketRow";
import { ticketDot } from "@/components/tickets/ticketDot";
import { age, ticketLabel } from "@/components/tickets/ticketFormat";
import { ticketBucketHref, TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { holdAsk, holdRowAct, yourMoveCount } from "@/components/tickets/holds/holdWords";
import { holdPullRequestHref } from "@/components/tickets/holds/holdRoutes";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";
import { useT } from "@/hooks/useT";
import type { TicketSummary } from "@/types/tickets";
import { HomeComposer } from "./HomeComposer";
import { MOVES_SHOWN, recentTickets, stateWords, waitingSentence, yourMoves } from "./homeItems";
import "@/styles/home.css";

// 2026-11-17-d6fba: the home page is input. One question and the composer that answers it, then what waits on the
// operator — at most three, the rest one click away on the ticket list — then the tickets that moved last. Both lists
// come from the ticket-list read the sidebar counts from, so the page and the Your move count cannot disagree.
// 2026-11-23-31dcg: each row says its hold's sentence (the question or hand-back itself where the hold is one) and its act.

export function HomeView() {
  const { list } = useTicketList();
  const admin = useHoldsAdmin();
  const tickets = list?.tickets ?? [];
  const waiting = yourMoveCount(list?.counts, admin) ?? 0;
  const moves = yourMoves(tickets, admin);
  const recent = recentTickets(tickets, admin);
  return (
    <div className="home" data-testid="home">
      <NewWorkHead />
      <HomeComposer />
      {moves.length > 0 && (
        <section className="home-sec" aria-labelledby="home-moves" data-testid="home-your-move">
          <h2 id="home-moves">Your move</h2>
          {moves.map((t) => <MoveRow key={`${t.project}/${t.ticketId}`} ticket={t} />)}
          {waiting > MOVES_SHOWN && (
            <Link className="home-more" href={ticketBucketHref("needs-you")} data-testid="home-your-move-all">
              All {waiting} that wait on you
            </Link>
          )}
        </section>
      )}
      {recent.length > 0 && (
        <section className="home-sec" aria-labelledby="home-recent" data-testid="home-recent">
          <h2 id="home-recent">Recent tickets</h2>
          {recent.map((t) => <RecentRow key={`${t.project}/${t.ticketId}`} ticket={t} />)}
          <Link className="home-more" href={TICKETS_PATH}>All tickets</Link>
        </section>
      )}
    </div>
  );
}

/** 2026-11-17-76f1: the question and the line under it — the home page's and a new Work it out conversation's. */
export function NewWorkHead() {
  const { list } = useTicketList();
  const admin = useHoldsAdmin();
  return (
    <header className="home-head">
      <h1>What should we work on?</h1>
      {list && <p className="home-sub" data-testid="home-waiting">{waitingSentence(yourMoveCount(list.counts, admin) ?? 0)}</p>}
    </header>
  );
}

function MoveRow({ ticket }: { ticket: TicketSummary }) {
  const t = useT();
  const act = holdRowAct(ticket.hold, t);
  return (
    <div className="home-row" data-testid={`home-move-${ticket.project}-${ticket.ticketId}`}>
      <span className={`home-dot ${ticketDot(ticket)}`} aria-hidden="true" />
      <div className="home-row-main">
        <div className="home-row-top">
          <span className="home-key">{ticketLabel(ticket)}</span>
          <span className="home-title">{ticket.title ?? ticket.ticketId}</span>
        </div>
        <div className="home-ask">{holdAsk(ticket, t)}</div>
      </div>
      <span className="home-age">{age(ticket.since) ?? ""}</span>
      {/* 2026-11-23-d60d: a pull request a person merges or whose CI failed opens on its host. */}
      <Link className="home-act" href={holdPullRequestHref(ticket.hold) ?? ticketHref(ticket)} data-testid="home-move-act">{act}</Link>
    </div>
  );
}

function RecentRow({ ticket }: { ticket: TicketSummary }) {
  return (
    <Link className="home-row link" href={ticketHref(ticket)} data-testid={`home-recent-${ticket.project}-${ticket.ticketId}`}>
      <span className={`home-dot ${ticketDot(ticket)}`} aria-hidden="true" />
      <span className="home-key">{ticketLabel(ticket)}</span>
      <span className="home-title grow">{ticket.title ?? ticket.ticketId}</span>
      <span className="home-state">{stateWords(ticket)}</span>
      <span className="home-age">{age(ticket.since) ?? ""}</span>
    </Link>
  );
}
