"use client";

import Link from "next/link";
import { useDateLocale, useT } from "@/hooks/useT";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import { conversationHref } from "@/components/dialog/conversationHref";
import { money } from "@/components/tickets/ticketFormat";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { seriesStateWord } from "../seriesNextStep";
import { openQuestionCount, SERIES_QUESTION_ID } from "./SeriesQuestionBlock";

// 2026-11-23-31dcd: the series' own bar beside the icon-only app sidebar, as the ticket page's: back to the list, the
// series' size, name and state (with the question that waits for a person), and at its foot the project, the repos, the
// conversation it came from, when it started and what it cost. 2026-11-25-9992j: no progress bar, counts or sections —
// the page is one pane, the graph says where the series stands.
// 2026-11-26-5201c: the questions that wait are a link to them, above the chat's input, and count every one drawn there.
// 2026-11-26-f511a: "N need you" — the block now holds approvals and failures too.

export function SeriesSectionBar({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const locale = useDateLocale();
  const questions = openQuestionCount(detail);
  const started = new Date(detail.startedAt).toLocaleDateString(locale, { day: "numeric", month: "short" });
  return (
    <aside className="tk-bar" aria-label={detail.name} data-testid="series-section-bar">
      <Link className="tk-bar-back" href={SERIES_PATH} data-testid="series-back">{t("series.page.back")}</Link>
      <div className="tk-bar-id" data-testid="series-head">
        <span className="tk-bar-key">{t("series.page.kind", { count: detail.counts.total })}</span>
        <span className="tk-bar-title">{detail.name}</span>
        <span className={`tk-bar-state ${stateTone(detail)}`} data-testid="series-state-word">
          {seriesStateWord(detail.state, t)}
          {questions > 0 && <> · <a href={`#${SERIES_QUESTION_ID}`} data-testid="series-question-link">{t("series.page.needsYou", { count: questions })}</a></>}
        </span>
      </div>
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
  if (openQuestionCount(detail) > 0) return "need";
  if (detail.state === "done") return "ok";
  if (detail.state === "stopped" || detail.state === "paused") return ""; // the bar's quiet colour
  return "run";
}
