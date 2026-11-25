"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { namesOf } from "../../tickets/holds/holdWords";
import { SeriesActButtons, type SeriesActHandlers } from "./SeriesActButtons";
import { seriesOverviewCounts, type SeriesNodeKind } from "./seriesNodeKind";
import { mergesWhenGreen } from "./seriesCiState";
import { seriesNodeSentence, seriesTicketActs } from "./seriesTicketActs";
import { SeriesTicketBadges } from "./SeriesTicketBadges";
import { SeriesFinalStep } from "./SeriesFinalStep";

// 2026-11-23-f562: below the graph, what someone has to do, in the graph's colours — You're up (the merge now, the
// questions, the failures), Needed next (the merges others start on, each with its Merge disabled and its pull request)
// and one line for the tickets that move by themselves. 2026-11-23-0219: a ready merge whose CI waits has its own group. Hovering or focusing a row lights its chain in the graph.

interface Props {
  detail: SeriesRunDetail;
  lacks: boolean;
  handlers: SeriesActHandlers;
  onChain: (ticketId: string | null) => void;
}

const YOURS: SeriesNodeKind[] = ["now", "question", "failed"];

export function SeriesActionList({ detail, lacks, handlers, onChain }: Props) {
  const t = useT();
  const counts = seriesOverviewCounts(detail, !lacks);
  const of = (kinds: SeriesNodeKind[]) => kinds.flatMap((kind) => detail.tickets.filter((ticket) => ticket.kind === kind));
  // 2026-11-23-8071: without runs.control the merge now is not the viewer's move — it leads Needed next, Merge disabled.
  const yours = of(lacks ? YOURS.filter((kind) => kind !== "now") : YOURS);
  const needed = of(lacks ? ["now", "needed"] : ["needed"]);
  // 2026-11-23-0219: a ready merge whose CI runs or has not started — Merge when CI is green — drawn with its act.
  const whenGreen = lacks ? [] : of(["pr"]).filter((ticket) => mergesWhenGreen(detail, ticket));
  const quiet = counts.quiet - whenGreen.length;
  const row = (ticket: SeriesRunTicket) => (
    <Row key={ticket.ticketId} detail={detail} ticket={ticket} lacks={lacks} handlers={handlers} onChain={onChain} />
  );
  return (
    <section className="sr-list" aria-label={t("series.todo.title")} data-testid="series-action-list">
      <div className="sr-list-group" data-testid="series-list-yours">{t("series.todo.yours")}</div>
      {yours.length > 0 ? yours.map(row) : <div className="sr-list-row quiet"><span className="sr-list-bar" /><div className="sr-list-sub">{t("series.todo.nothing")}</div></div>}
      {needed.length > 0 && (
        <>
          <div className="sr-list-group" data-testid="series-list-needed">
            {t("series.todo.needed", { names: namesOf(t, counts.neededFor), count: counts.neededFor.length })}
          </div>
          {needed.map(row)}
        </>
      )}
      {whenGreen.length > 0 && (
        <>
          <div className="sr-list-group" data-testid="series-list-when-green">
            {t(detail.mergeArms ? "series.todo.whenGreen" : "series.todo.whenGreenUnarmed")}
          </div>
          {whenGreen.map(row)}
        </>
      )}
      {quiet > 0 && (
        <>
          <div className="sr-list-group">{t("series.todo.quietTitle")}</div>
          <div className="sr-list-row quiet" data-testid="series-list-quiet">
            <span className="sr-list-bar" />
            <div>
              <div className="sr-list-title">{t("series.todo.quiet", { count: quiet })}</div>
              <div className="sr-list-sub">{t("series.todo.quietSub")}</div>
            </div>
          </div>
        </>
      )}
      <SeriesFinalStep detail={detail} />
    </section>
  );
}

function Row({ detail, ticket, lacks, handlers, onChain }: { detail: SeriesRunDetail; ticket: SeriesRunTicket; lacks: boolean;
  handlers: SeriesActHandlers; onChain: (ticketId: string | null) => void }) {
  const t = useT();
  const kind = ticket.kind;
  return (
    <div className={`sr-list-row ${kind}`} data-testid={`series-row-${ticket.ticketId}`} data-kind={kind}
      onMouseEnter={() => onChain(ticket.ticketId)} onMouseLeave={() => onChain(null)}
      onFocus={() => onChain(ticket.ticketId)} onBlur={() => onChain(null)}>
      <span className="sr-list-bar" />
      <div className="sr-list-main">
        <div className="sr-list-title">
          <Link className="mono" href={ticketPath(detail.project, ticket.ticketId)}>{ticket.key}</Link> {ticket.title} <SeriesTicketBadges ticket={ticket} />
        </div>
        <div className="sr-list-sub" data-testid={`series-row-say-${ticket.ticketId}`}>{seriesNodeSentence(ticket, detail, kind, t)}</div>
      </div>
      <SeriesActButtons ticket={ticket} acts={seriesTicketActs(ticket, detail, kind, lacks, t)} handlers={handlers} row testId="series-row" />
    </div>
  );
}
