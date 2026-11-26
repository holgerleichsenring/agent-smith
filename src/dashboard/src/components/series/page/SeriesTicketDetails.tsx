"use client";

import Link from "next/link";
import { useEffect, useRef, type CSSProperties } from "react";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunTicket } from "@/types/seriesRuns";
import { holdSentence } from "../../tickets/holds/holdWords";
import { ago, money } from "../../tickets/ticketFormat";
import { seriesNodeId } from "./seriesOrderLayout";

// 2026-11-25-9992j: what a click on a graph node opens — the ticket to read, nothing to operate: its key and title, its
// state in the graph's words and the hold's sentence, each pull request with its CI and conflict, what it cost, and the
// way to its page. Acts are the master's, asked in the chat. Focus moves into it; Escape closes it and gives focus back
// to the node, a click outside closes it. 2026-11-25-9992e: and the series master's last act on it — what, why, when.
// 2026-11-26-5201f: a pull request the host still needs a person's approval on says so beside its link.

interface Props {
  ticket: SeriesRunTicket;
  project: string;
  /** The series branch, so a pull request into it says so (2026-11-24-f9c5f); null without one. */
  integrationBranch?: string | null;
  at: CSSProperties;
  onClose: () => void;
}

const CI_WORDS: Record<string, TextKey> = {
  none: "series.details.ci.none", pending: "series.details.ci.pending", not_started: "series.details.ci.not_started",
  green: "series.details.ci.green", red: "series.details.ci.red",
};

export function SeriesTicketDetails({ ticket, project, integrationBranch = null, at, onClose }: Props) {
  const t = useT();
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
    ref.current?.querySelector<HTMLElement>("a, button")?.focus();
    return () => { document.removeEventListener("keydown", key); document.removeEventListener("mousedown", down); };
  }, [ticket.ticketId, onClose]);
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
      <Link className="tk-link" href={ticketPath(project, ticket.ticketId)} data-testid={`series-details-open-${ticket.ticketId}`}>{t("hold.act.open")}</Link>
    </div>
  );
}
