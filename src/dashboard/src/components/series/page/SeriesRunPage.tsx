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
import { SeriesSectionBar } from "./SeriesSectionBar";
import { useSeriesRunDetail } from "./useSeriesRunDetail";

// 2026-11-23-31dcd: one series, built like the ticket page — its bar beside the icon-only sidebar.
// 2026-11-25-9992j: ONE pane — the graph (what the series stands at) and the series chat below it, the page's only input.
// The sections are gone; an older ?section=talk link scrolls to the chat, any other name lands on the top.

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
  const toChat = params?.get("section") === "talk";
  useEffect(() => { if (toChat) chat.current?.scrollIntoView?.({ block: "start" }); }, [toChat]);
  return (
    <div className="mock-shell mock-tickets tk-shell" data-testid="series-page">
      <SeriesSectionBar detail={detail} />
      <main className="main tk-page">
        <div className="tk-heading"><h1 data-testid="series-title">{detail.name}</h1></div>
        <section className="tk-pane" data-testid="series-pane">
          <SeriesOverviewSection detail={detail} />
          <section ref={chat} id={SERIES_CHAT_ID} className="sr-chat" aria-label={t("series.chat.title")} data-testid="series-chat">
            <h2>{t("series.chat.title")}</h2>
            <SeriesConversationSection detail={detail} />
          </section>
        </section>
      </main>
    </div>
  );
}
