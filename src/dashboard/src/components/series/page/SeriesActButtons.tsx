"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import type { SeriesAct } from "./seriesTicketActs";

// 2026-11-23-f562: one ticket's acts drawn as buttons — the node menu draws all of them, an action-list row those not
// marked menuOnly. Merge and Retry post their acts, a link follows its route, Show chain lights the ticket's course.

export interface SeriesActHandlers {
  mergeBusy: string | null;
  retryBusy: string | null;
  onMerge: (ticketId: string) => void;
  onRetry: (ticketId: string, runId: string) => void;
  onChain: (ticketId: string) => void;
}

interface Props {
  ticket: SeriesRunTicket;
  acts: SeriesAct[];
  handlers: SeriesActHandlers;
  /** The row draws only the acts that are not the menu's alone. */
  row?: boolean;
  testId: string;
}

export function SeriesActButtons({ ticket, acts, handlers, row = false, testId }: Props) {
  const t = useT();
  const shown = row ? acts.filter((act) => !act.menuOnly) : acts;
  const busy = handlers.mergeBusy !== null || handlers.retryBusy !== null;
  return (
    <div className="sr-acts">
      {shown.map((act) => {
        const className = `btn sm${act.tone ? ` ${act.tone}` : ""}`;
        const id = `${testId}-${act.id}-${ticket.ticketId}`;
        if (act.kind === "external")
          return <a key={act.id} className={className} href={act.href} target="_blank" rel="noreferrer" data-testid={id}>{act.label}</a>;
        if (act.kind === "link")
          return <Link key={act.id} className={className} href={act.href!} data-testid={id}>{act.label}</Link>;
        if (act.kind === "chain")
          return <button key={act.id} type="button" className={className} onClick={() => handlers.onChain(ticket.ticketId)} data-testid={id}>{act.label}</button>;
        const merge = act.kind === "merge";
        const label = merge && handlers.mergeBusy === ticket.ticketId ? t("series.merge.busy")
          : !merge && handlers.retryBusy === ticket.ticketId ? t("series.act.retrying") : act.label;
        return (
          <button key={act.id} type="button" className={className} disabled={!!act.disabledBy || busy}
            title={act.disabledBy} aria-label={act.disabledBy ? `${act.label}: ${act.disabledBy}` : undefined}
            onClick={() => (merge ? handlers.onMerge(ticket.ticketId) : handlers.onRetry(ticket.ticketId, act.run!))}
            data-testid={id}>
            {label}
          </button>
        );
      })}
    </div>
  );
}
