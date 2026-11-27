"use client";

import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useT } from "@/hooks/useT";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import "@/styles/ticket-thread.css";
import "@/styles/series.css";
import { SeriesConversationSection } from "./SeriesConversationSection";
import { SERIES_OVERVIEW_ID, SeriesOverviewSection } from "./SeriesOverviewSection";
import { SERIES_QUESTION_ID } from "./SeriesQuestionBlock";
import { SeriesSectionBar } from "./SeriesSectionBar";
import { SeriesStatusLine } from "./SeriesStatusLine";
import { SeriesTicketDetails } from "./SeriesTicketDetails";
import { SeriesTicketList } from "./SeriesTicketList";
import { menuBelow, menuBeside, type MenuAt } from "./seriesMenuAt";
import { seriesCourse } from "./seriesOrderLayout";
import { useSeriesGraphCollapsed } from "./useSeriesGraphCollapsed";
import { useSeriesGraphSplit } from "./useSeriesGraphSplit";
import { useSeriesRunDetail } from "./useSeriesRunDetail";

// 2026-11-23-31dcd: one series, built like the ticket page — its bar beside the icon-only sidebar.
// 2026-11-25-9992j: ONE pane — the graph (what the series stands at) and the series chat below it, the page's only input.
// The sections are gone; an older ?section=talk link scrolls to the chat, any other name lands on the top.
// 2026-11-25-433fa: the chat opens with the series' status line, pinned while the chat scrolls.
// 2026-11-26-5201a: the page is the viewport's height — the graph on top, the chat filling the rest, scrolling by itself.
// 2026-11-26-b186: no chrome over the chat — the status line sits in the title row, the "Chat" heading is gone, and the
// graph folds to one line on request, remembered per browser.
// 2026-11-26-f511b: a divider between the graph and the chat sets the graph's height, by pointer or keys, remembered per browser.
// 2026-11-27-ecf2a: the rail is the series' name and its ticket list; the main column's top row keeps the status line.

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

interface Opened { ticketId: string; from: "node" | "row"; anchor: HTMLElement }

function SeriesPane({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const params = useSearchParams();
  const chat = useRef<HTMLElement>(null);
  const pane = useRef<HTMLElement>(null);
  const [collapsed, toggleGraph] = useSeriesGraphCollapsed();
  const split = useSeriesGraphSplit(pane, collapsed, detail);
  // 2026-11-27-ecf2a: the rail row pointed at lights its course in the graph; a node or a row opens the same details.
  const [hovered, setHovered] = useState<string | null>(null);
  const [opened, setOpened] = useState<Opened | null>(null);
  const [at, setAt] = useState<MenuAt | null>(null);
  const close = useCallback(() => setOpened(null), []);
  // A row's open details keep its course lit — opening them moves focus off the row, which would clear it.
  const coursed = hovered ?? (opened?.from === "row" ? opened.ticketId : null);
  const lit = useMemo(() => (coursed ? seriesCourse(detail.edges, coursed) : null), [detail.edges, coursed]);
  const openTicket = opened ? detail.tickets.find((ticket) => ticket.ticketId === opened.ticketId) : undefined;
  useLayoutEffect(() => {
    if (!opened) { setAt(null); return; }
    const own = opened.anchor.getBoundingClientRect();
    setAt(opened.from === "row" ? menuBeside(own) : menuBelow(own));
  }, [opened, detail]);
  const toggle = useCallback((ticketId: string, from: Opened["from"], anchor: HTMLElement) =>
    setOpened((open) => (open?.ticketId === ticketId && open.from === from ? null : { ticketId, from, anchor })), []);
  const onNode = useCallback((ticketId: string, anchor: HTMLElement) => toggle(ticketId, "node", anchor), [toggle]);
  const onRow = useCallback((ticketId: string, anchor: HTMLElement) => toggle(ticketId, "row", anchor), [toggle]);
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
      <SeriesSectionBar detail={detail}>
        <SeriesTicketList detail={detail} hovered={hovered} onHover={setHovered} onOpen={onRow} />
      </SeriesSectionBar>
      <main className="main tk-page">
        <div className="tk-heading sr-heading">
          <SeriesStatusLine detail={detail} />
        </div>
        <section ref={pane} className="tk-pane" data-testid="series-pane"
          style={split.height === null ? undefined : ({ "--sr-graph-h": `${split.height}px` } as CSSProperties)}>
          <SeriesOverviewSection detail={detail} collapsed={collapsed} onToggle={toggleGraph} lit={lit} hovered={hovered}
            openId={opened?.ticketId ?? null} onNode={onNode} onClose={close} />
          {!collapsed && (
            <div {...split.separator} className="sr-divider" aria-controls={SERIES_OVERVIEW_ID} aria-label={t("series.graph.resize")}
              data-testid="series-graph-divider" />
          )}
          <section ref={chat} id={SERIES_CHAT_ID} className="sr-chat" aria-label={t("series.chat.title")} data-testid="series-chat">
            <SeriesConversationSection detail={detail} />
          </section>
        </section>
      </main>
      {opened && openTicket && at && (
        <SeriesTicketDetails ticket={openTicket} project={detail.project} seriesId={detail.id} integrationBranch={detail.integrationBranch}
          at={at} anchor={opened.anchor} onClose={close} />
      )}
    </div>
  );
}
