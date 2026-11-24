"use client";

import Link from "next/link";
import { ticketHref } from "@/components/tickets/TicketRow";
import { ticketDot } from "@/components/tickets/ticketDot";
import { compactAge, ticketLabel } from "@/components/tickets/ticketFormat";
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
  const t = useT();
  return (
    <div className="home" data-testid="home">
      <NewWorkHead />
      <HomeComposer />
      {moves.length > 0 && (
        <section className="home-sec" aria-labelledby="home-moves" data-testid="home-your-move">
          <h2 id="home-moves">{t("shell.nav.yourMove")}</h2>
          {moves.map((m) => <MoveRow key={`${m.project}/${m.ticketId}`} ticket={m} />)}
          {waiting > MOVES_SHOWN && (
            <Link className="home-more" href={ticketBucketHref("needs-you")} data-testid="home-your-move-all">
              {t("home.allWaiting", { count: waiting })}
            </Link>
          )}
        </section>
      )}
      {recent.length > 0 && (
        <section className="home-sec" aria-labelledby="home-recent" data-testid="home-recent">
          <h2 id="home-recent">{t("home.recentTickets")}</h2>
          {recent.map((r) => <RecentRow key={`${r.project}/${r.ticketId}`} ticket={r} />)}
          <Link className="home-more" href={TICKETS_PATH}>{t("home.allTickets")}</Link>
        </section>
      )}
    </div>
  );
}

/** 2026-11-17-76f1: the question and the line under it — the home page's and a new Work it out conversation's. */
export function NewWorkHead() {
  const { list } = useTicketList();
  const admin = useHoldsAdmin();
  const t = useT();
  return (
    <header className="home-head">
      <h1>{t("home.question")}</h1>
      {list && <p className="home-sub" data-testid="home-waiting">{waitingSentence(t, yourMoveCount(list.counts, admin) ?? 0)}</p>}
    </header>
  );
}

function MoveRow({ ticket }: { ticket: TicketSummary }) {
  const t = useT();
  const act = holdRowAct(t, ticket.hold);
  return (
    <div className="home-row" data-testid={`home-move-${ticket.project}-${ticket.ticketId}`}>
      <span className={`home-dot ${ticketDot(ticket)}`} aria-hidden="true" />
      <div className="home-row-main">
        <div className="home-row-top">
          <span className="home-key">{ticketLabel(ticket)}</span>
          <span className="home-title">{ticket.title ?? ticket.ticketId}</span>
        </div>
        <div className="home-ask">{holdAsk(t, ticket)}</div>
      </div>
      <span className="home-age">{compactAge(t, ticket.since) ?? ""}</span>
      {/* 2026-11-23-d60d: a pull request a person merges or whose CI failed opens on its host. */}
      <Link className="home-act" href={holdPullRequestHref(ticket.hold) ?? ticketHref(ticket)} data-testid="home-move-act">{act}</Link>
    </div>
  );
}

function RecentRow({ ticket }: { ticket: TicketSummary }) {
  const t = useT();
  return (
    <Link className="home-row link" href={ticketHref(ticket)} data-testid={`home-recent-${ticket.project}-${ticket.ticketId}`}>
      <span className={`home-dot ${ticketDot(ticket)}`} aria-hidden="true" />
      <span className="home-key">{ticketLabel(ticket)}</span>
      <span className="home-title grow">{ticket.title ?? ticket.ticketId}</span>
      <span className="home-state">{stateWords(ticket, t)}</span>
      <span className="home-age">{compactAge(t, ticket.since) ?? ""}</span>
    </Link>
  );
}
