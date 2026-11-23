"use client";

import { useEffect, useRef, type CSSProperties } from "react";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import { SeriesActButtons, type SeriesActHandlers } from "./SeriesActButtons";
import type { SeriesNodeKind } from "./seriesNodeKind";
import type { SeriesAct } from "./seriesTicketActs";
import { SeriesTicketBadges } from "./SeriesTicketBadges";
import { seriesNodeId } from "./seriesOrderLayout";

// 2026-11-23-f562: the small menu a click on a graph node opens — the ticket, one sentence why it has its colour, and its
// acts. Focus moves into it; Escape there closes it and gives focus back to the node, a click outside closes it.

interface Props {
  ticket: SeriesRunTicket;
  kind: SeriesNodeKind;
  sentence: string;
  acts: SeriesAct[];
  handlers: SeriesActHandlers;
  at: CSSProperties;
  onClose: () => void;
}

export function SeriesNodeMenu({ ticket, kind, sentence, acts, handlers, at, onClose }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const node = () => document.getElementById(seriesNodeId(ticket.ticketId));
    const key = (e: KeyboardEvent) => {
      const inside = ref.current?.contains(document.activeElement) || node() === document.activeElement;
      if (e.key === "Escape" && inside) { onClose(); node()?.focus(); }
    };
    const down = (e: MouseEvent) => {
      const target = e.target as Node;
      if (!ref.current?.contains(target) && !node()?.contains(target)) onClose();
    };
    document.addEventListener("keydown", key);
    document.addEventListener("mousedown", down);
    ref.current?.querySelector<HTMLElement>("a, button:not(:disabled)")?.focus();
    return () => { document.removeEventListener("keydown", key); document.removeEventListener("mousedown", down); };
  }, [ticket.ticketId, onClose]);
  return (
    <div ref={ref} className={`sr-menu ${kind}`} style={at} role="dialog" aria-label={`${ticket.key} ${ticket.title}`}
      data-testid={`series-menu-${ticket.ticketId}`}>
      <div className="sr-menu-title">{ticket.key} {ticket.title} <SeriesTicketBadges ticket={ticket} /></div>
      <div className="sr-menu-why" data-testid={`series-menu-say-${ticket.ticketId}`}>{sentence}</div>
      <SeriesActButtons ticket={ticket} acts={acts} handlers={handlers} testId="series-menu" />
    </div>
  );
}
