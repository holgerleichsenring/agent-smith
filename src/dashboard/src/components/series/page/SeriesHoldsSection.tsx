"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { holdRowAct, holdSentence, holdTag } from "../../tickets/holds/holdWords";
import { holdSeriesHref } from "../../tickets/holds/holdRoutes";
import { SeriesChangesNotes } from "./SeriesChangesNotes";

// 2026-11-23-31dcg: what holds the series' tickets that are not merging next — each ticket whose hold names someone, with
// whose it is, the hold's sentence (the one its ticket page says) and its act; the ones nobody needs to move are one line
// at the end. The merge that comes next is Merge next's, so it is not repeated here.
// 2026-11-23-31dcj: below them, what changed in the tickets' spec sets (SeriesChangesNotes), read when the pane opens.

export function SeriesHoldsSection({ detail }: { detail: SeriesRunDetail }) {
  return (
    <>
      <SeriesHolds detail={detail} />
      <SeriesChangesNotes detail={detail} />
    </>
  );
}

function SeriesHolds({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const held = detail.tickets.filter((ticket) => ticket.isHeld && ticket.hold);
  const moving = detail.tickets.filter((ticket) => ticket.hold?.addressee === "nobody");
  if (held.length === 0 && moving.length === 0) return <p className="msub" data-testid="series-holds-empty">{t("series.holds.empty")}</p>;
  return (
    <>
      <p className="msub">{t("series.holds.sub")}</p>
      <ul className="sr-holds" data-testid="series-holds">
        {held.map((ticket) => <HeldRow key={ticket.ticketId} detail={detail} ticket={ticket} />)}
        {moving.length > 0 && (
          <li data-testid="series-holds-moving">
            <div className="sr-holds-head">
              <span>{t("series.holds.more", { count: moving.length })}</span>
              <span className="sr-holds-tag">{t("hold.tag.nobody")}</span>
            </div>
            <div className="sr-holds-say">{t("series.holds.moreSub")}</div>
          </li>
        )}
      </ul>
    </>
  );
}

function HeldRow({ detail, ticket }: { detail: SeriesRunDetail; ticket: SeriesRunTicket }) {
  const t = useT();
  const hold = ticket.hold!;
  const href = holdSeriesHref(hold, seriesRunPath) ?? ticketPath(detail.project, ticket.ticketId);
  return (
    <li className={hold.addressee} data-testid={`series-hold-${ticket.ticketId}`} data-addressee={hold.addressee}>
      <div className="sr-holds-head">
        <span><Link className="mono" href={ticketPath(detail.project, ticket.ticketId)}>{ticket.key}</Link> {ticket.title}</span>
        <span className="sr-holds-tag">{holdTag(hold, t)}</span>
      </div>
      <div className="sr-holds-say" data-testid={`series-hold-sentence-${ticket.ticketId}`}>{holdSentence(hold, t)}</div>
      <div><Link className="btn sm primary" href={href} data-testid={`series-hold-act-${ticket.ticketId}`}>{holdRowAct(hold, t)}</Link></div>
    </li>
  );
}
