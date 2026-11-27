"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type CSSProperties } from "react";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { startSeriesTicket } from "@/lib/seriesRunsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import { holdSentence } from "../../tickets/holds/holdWords";
import { ago, money } from "../../tickets/ticketFormat";
import { seriesNodeId, seriesNodeLinksId } from "./seriesOrderLayout";

// 2026-11-25-9992j: what a click on a graph node opens — the ticket to read, nothing to operate: its key and title, its
// state in the graph's words and the hold's sentence, each pull request with its CI and conflict, what it cost, and the
// way to its page. Acts are the master's, asked in the chat. Focus moves into it; Escape closes it and gives focus back
// to the node, a click outside closes it. 2026-11-25-9992e: and the series master's last act on it — what, why, when.
// 2026-11-26-5201e: and the ticket in its tracker, in a new tab, beside its page here.
// 2026-11-26-5201f: a pull request the host still needs a person's approval on says so beside its link.
// 2026-11-26-5f6e: a ticket no run worked yet has one act, Start — a ticket the master added has no run, so the ticket list
// does not show it and this is where a person starts it; the answer (started, queued or refused, and why) is shown as is.

interface Props {
  ticket: SeriesRunTicket;
  project: string;
  /** The series, so Start reaches its member ticket (2026-11-26-5f6e). */
  seriesId?: string;
  /** The series branch, so a pull request into it says so (2026-11-24-f9c5f); null without one. */
  integrationBranch?: string | null;
  at: CSSProperties;
  onClose: () => void;
  /** 2026-11-27-ecf2a: what opened it — a graph node or a row of the rail's list; Escape gives focus back to it. */
  anchor?: HTMLElement | null;
}

const CI_WORDS: Record<string, TextKey> = {
  none: "series.details.ci.none", pending: "series.details.ci.pending", not_started: "series.details.ci.not_started",
  green: "series.details.ci.green", red: "series.details.ci.red",
};

export function SeriesTicketDetails({ ticket, project, seriesId, integrationBranch = null, at, onClose, anchor = null }: Props) {
  const t = useT();
  const ref = useRef<HTMLDivElement>(null);
  const [starting, setStarting] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const startable = !!seriesId && !ticket.landed && ticket.status === null && !ticket.working;
  const start = async () => {
    setStarting(true);
    try { setSaid((await startSeriesTicket(seriesId!, ticket.ticketId)).text); }
    catch (e) { setSaid(e instanceof Error ? e.message : String(e)); }
    finally { setStarting(false); }
  };
  useEffect(() => {
    const node = () => document.getElementById(seriesNodeId(ticket.ticketId));
    const key = (e: KeyboardEvent) => {
      const inside = ref.current?.contains(document.activeElement) || node() === document.activeElement
        || (!!anchor && anchor === document.activeElement);
      if (e.key === "Escape" && inside) { onClose(); (anchor ?? node())?.focus(); }
    };
    const down = (e: MouseEvent) => {
      const target = e.target as Node;
      const links = document.getElementById(seriesNodeLinksId(ticket.ticketId));
      if (!ref.current?.contains(target) && !node()?.contains(target) && !links?.contains(target) && !anchor?.contains(target)) onClose();
    };
    // 2026-11-26-b186: placed in viewport coordinates, so any scroll (the overview's, the page's) leaves it behind — close it.
    const scrolled = (e: Event) => { if (!ref.current?.contains(e.target as Node)) onClose(); };
    document.addEventListener("keydown", key);
    document.addEventListener("mousedown", down);
    document.addEventListener("scroll", scrolled, true);
    ref.current?.querySelector<HTMLElement>("a, button")?.focus();
    return () => {
      document.removeEventListener("keydown", key); document.removeEventListener("mousedown", down);
      document.removeEventListener("scroll", scrolled, true);
    };
  }, [ticket.ticketId, onClose, anchor]);
  const why = ticket.kind === "merged" ? t("series.node.merged") : ticket.hold ? holdSentence(t, ticket.hold) : t("series.node.notStarted");
  return (
    <div ref={ref} className={`sr-menu ${ticket.kind}`} style={at} role="dialog" aria-label={`${ticket.key} ${ticket.title}`}
      data-testid={`series-details-${ticket.ticketId}`}>
      <div className="sr-menu-title">{ticket.key} {ticket.title}</div>
      <div className="sr-menu-why" data-testid={`series-details-state-${ticket.ticketId}`}>
        <b>{t(`series.kind.${ticket.kind}`)}</b>{ticket.working ? ` · ${t("series.graph.working")}` : ""} — {why}
      </div>
      {ticket.pullRequests.length > 0 && (
        <ul className="sr-details-prs">
          {ticket.pullRequests.map((pr) => (
            <li key={pr.url} data-testid={`series-details-pr-${pr.repo}-${ticket.ticketId}`}>
              <a href={pr.url} target="_blank" rel="noreferrer">{t("series.merge.pr", { number: pr.number })}</a>
              <span className="mono"> {pr.repo}</span>
              {pr.target && <span> → {pr.target === integrationBranch ? t("series.tickets.intoSeries") : pr.target}</span>}
              {pr.ci && CI_WORDS[pr.ci] && <span> · {t(CI_WORDS[pr.ci])}</span>}
              {pr.hasConflicts && <span className="sr-state bad"> · {t("series.badge.conflict")}</span>}
              {pr.isDraft && <span className="sr-state person" data-testid={`series-details-draft-${pr.repo}-${ticket.ticketId}`}> · {t("series.details.draft")}</span>}
              {pr.approvalRequired && <span className="sr-state person" data-testid={`series-details-approval-${pr.repo}-${ticket.ticketId}`}> · {t("series.details.approval")}</span>}
              {pr.ci === "red" && pr.ciFailure && <div className="sr-menu-why">{pr.ciFailure}</div>}
            </li>
          ))}
        </ul>
      )}
      {ticket.lastAct && (
        <div className="sr-menu-why" data-testid={`series-details-lastact-${ticket.ticketId}`}>
          {t("series.details.lastAct", { when: ago(t, ticket.lastAct.at) ?? "", text: ticket.lastAct.text })}
          {ticket.lastAct.reason && <span> — {ticket.lastAct.reason}</span>}
        </div>
      )}
      <div className="sr-menu-why" data-testid={`series-details-cost-${ticket.ticketId}`}>{t("series.details.cost", { cost: money(ticket.costUsd) })}</div>
      {said && <div className="sr-menu-why" role="status" data-testid={`series-details-start-said-${ticket.ticketId}`}>{said}</div>}
      <div className="sr-menu-links">
        {startable && <button type="button" className="btn sm" disabled={starting} onClick={start}
          data-testid={`series-details-start-${ticket.ticketId}`}>{starting ? t("series.details.starting") : t("series.details.start")}</button>}
        <Link className="tk-link" href={ticketPath(project, ticket.ticketId)} data-testid={`series-details-open-${ticket.ticketId}`}>{t("hold.act.open")}</Link>
        {ticket.webUrl && <a className="tk-link" href={ticket.webUrl} target="_blank" rel="noreferrer"
          data-testid={`series-details-tracker-${ticket.ticketId}`}>{t("series.details.tracker")}</a>}
      </div>
    </div>
  );
}
