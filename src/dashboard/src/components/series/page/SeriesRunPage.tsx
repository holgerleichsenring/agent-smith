"use client";

import { useEffect, useRef } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useT } from "@/hooks/useT";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import "@/styles/ticket-thread.css";
import "@/styles/series.css";
import { SeriesConversationSection } from "./SeriesConversationSection";
import { SeriesOverviewSection } from "./SeriesOverviewSection";
import { SERIES_QUESTION_ID } from "./SeriesQuestionBlock";
import { SeriesSectionBar } from "./SeriesSectionBar";
import { SeriesStatusLine } from "./SeriesStatusLine";
import { useSeriesGraphCollapsed } from "./useSeriesGraphCollapsed";
import { useSeriesRunDetail } from "./useSeriesRunDetail";

// 2026-11-23-31dcd: one series, built like the ticket page — its bar beside the icon-only sidebar.
// 2026-11-25-9992j: ONE pane — the graph (what the series stands at) and the series chat below it, the page's only input.
// The sections are gone; an older ?section=talk link scrolls to the chat, any other name lands on the top.
// 2026-11-25-433fa: the chat opens with the series' status line, pinned while the chat scrolls.
// 2026-11-26-5201a: the page is the viewport's height — the graph on top, the chat filling the rest, scrolling by itself.
// 2026-11-26-b186: no chrome over the chat — the status line sits in the title row, the "Chat" heading is gone, and the
// graph folds to one line on request, remembered per browser.

export const SERIES_CHAT_ID = "series-chat";

export function SeriesRunPage({ id }: { id: string }) {
  const { read } = useSeriesRunDetail(id);
  const t = useT();
  if (read.kind !== "read") {
    return (
      <div className="mock-shell mock-tickets" data-testid="series-page">
        <main className="main tk-page">
          <div className="msub" data-testid="series-page-unread">
            {read.kind === "loading" ? t("series.page.loading") : read.kind === "notFound" ? t("series.page.notFound") : t("series.page.failed")}
            {read.kind !== "loading" && <> <Link href={SERIES_PATH}>{t("series.page.backToList")}</Link></>}
          </div>
        </main>
      </div>
    );
  }
  return <SeriesPane detail={read.detail} />;
}

function SeriesPane({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const params = useSearchParams();
  const chat = useRef<HTMLElement>(null);
  const [collapsed, toggleGraph] = useSeriesGraphCollapsed();
  const section = params?.get("section");
  const toChat = section === "talk" || section === "question";
  // 2026-11-26-5201c: a link to the question (the list's row, a hold's) lands on the question block when one waits.
  useEffect(() => {
    if (!toChat) return;
    const question = document.getElementById(SERIES_QUESTION_ID);
    if (question) question.scrollIntoView?.({ block: "nearest" });
    else chat.current?.scrollIntoView?.({ block: "start" });
  }, [toChat]);
  return (
    <div className="mock-shell mock-tickets tk-shell sr-page" data-testid="series-page">
      <SeriesSectionBar detail={detail} />
      <main className="main tk-page">
        <div className="tk-heading sr-heading">
          <h1 data-testid="series-title">{detail.name}</h1>
          <SeriesStatusLine detail={detail} />
        </div>
        <section className="tk-pane" data-testid="series-pane">
          <SeriesOverviewSection detail={detail} collapsed={collapsed} onToggle={toggleGraph} />
          <section ref={chat} id={SERIES_CHAT_ID} className="sr-chat" aria-label={t("series.chat.title")} data-testid="series-chat">
            <SeriesConversationSection detail={detail} />
          </section>
        </section>
      </main>
    </div>
  );
}
