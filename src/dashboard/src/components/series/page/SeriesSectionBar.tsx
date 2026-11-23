"use client";

import Link from "next/link";
import { useLanguage, useT } from "@/hooks/useT";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import { conversationHref } from "@/components/dialog/conversationHref";
import { money } from "@/components/tickets/ticketFormat";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { SeriesProgress } from "../SeriesProgress";
import { seriesStateWord } from "../seriesNextStep";
import { seriesSections, type SeriesSection } from "./seriesPageSections";

// 2026-11-23-31dcd: the series' own bar beside the icon-only app sidebar, as the ticket page's: back to the list, the
// series' size, name and state (with the question that waits for a person), its progress, the sections with their
// counts, and at its foot the project, the repos, the conversation it came from, when it started and what it cost.

interface Props {
  detail: SeriesRunDetail;
  current: SeriesSection;
  onSelect: (id: string) => void;
}

export function SeriesSectionBar({ detail, current, onSelect }: Props) {
  const t = useT();
  const language = useLanguage();
  const started = new Date(detail.startedAt).toLocaleDateString(language === "de" ? "de-DE" : "en-GB", { day: "numeric", month: "short" });
  return (
    <aside className="tk-bar" aria-label={detail.name} data-testid="series-section-bar">
      <Link className="tk-bar-back" href={SERIES_PATH} data-testid="series-back">{t("series.page.back")}</Link>
      <div className="tk-bar-id" data-testid="series-head">
        <span className="tk-bar-key">{t("series.page.kind", { count: detail.counts.total })}</span>
        <span className="tk-bar-title">{detail.name}</span>
        <span className={`tk-bar-state ${stateTone(detail)}`} data-testid="series-state-word">
          {seriesStateWord(detail.state, t)}{detail.question ? ` · ${t("series.page.question")}` : ""}
        </span>
      </div>
      <div className="sr-bar-head"><SeriesProgress counts={detail.counts} /></div>
      <div className="sr-bar-counts" data-testid="series-counts">{t("series.counts", { ...detail.counts })}</div>
      <nav className="tk-bar-sections" aria-label={t("series.page.sections")}>
        {seriesSections.map((section) => {
          const counter = section.counter(detail);
          return (
            <button key={section.id} type="button" className="tk-bar-sec" aria-current={section.id === current.id ? "page" : undefined}
              onClick={() => onSelect(section.id)} data-testid={`series-section-${section.id}`}>
              <span className="tk-bar-label">{t(section.label)}</span>
              {counter && <span className={`tk-bar-count${counter.hot ? " hot" : ""}`}>{counter.text}</span>}
            </button>
          );
        })}
      </nav>
      <div className="tk-bar-facts" data-testid="series-facts">
        <span>{t("series.fact.project", { project: detail.project })}</span>
        {detail.repos.length > 0 && <span data-testid="series-repos">{detail.repos.join(" · ")}</span>}
        <Link href={conversationHref({ sessionId: detail.conversation, openDialogId: detail.conversationOpen ? detail.threadId : null })}
          data-testid="series-conversation-link">{t("series.fact.conversation")}</Link>
        <span>{t("series.fact.started", { date: started, cost: money(detail.costUsd) })}</span>
      </div>
    </aside>
  );
}

// The state word's colour: a question waits for a person; otherwise done passed, stopped or paused is quiet, the rest moves.
function stateTone(detail: SeriesRunDetail): string {
  if (detail.question) return "need";
  if (detail.state === "done") return "ok";
  if (detail.state === "stopped" || detail.state === "paused") return ""; // the bar's quiet colour
  return "run";
}
