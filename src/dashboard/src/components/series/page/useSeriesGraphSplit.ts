"use client";

import { useCallback, useEffect, useRef, useState, type KeyboardEvent, type PointerEvent, type RefObject } from "react";

// 2026-11-26-f511b: the divider between the series graph and the chat. The viewer sets the graph's share of the pane (by
// default 40%), remembered per browser like the fold (useSeriesGraphCollapsed): read after mount, every access in try/catch.
// The drawn height is that share, never more than the graph's own height, and never so much that the chat loses its question
// block, its composer and a few lines of conversation; the graph's head (and its Hide button) always stays.

const KEY = "agentsmith.series-graph-split";
export const GRAPH_SHARE_DEFAULT = 0.4;
/** The graph's head: never drawn smaller, so its Hide button stays reachable — this floor wins over the chat's room. */
export const GRAPH_MIN_PX = 48;
/** A few lines of conversation the chat keeps besides its question block and composer. */
export const CHAT_LINES_PX = 96;
const STEP = 0.05;

export interface SeriesGraphSizes {
  /** The pane's height: graph, divider and chat. 0 while unmeasured. */
  pane: number;
  /** The graph's own height, all of it shown. */
  natural: number;
  /** The chat's parts that never shrink: the question block, the composer, the lines between. */
  chatFixed: number;
}

/** The smallest and largest height the graph may be drawn at. */
export function seriesGraphBounds({ pane, natural, chatFixed }: SeriesGraphSizes): { min: number; max: number } {
  const max = Math.min(natural, pane - chatFixed - CHAT_LINES_PX);
  return { min: GRAPH_MIN_PX, max: Math.max(GRAPH_MIN_PX, max) };
}

/** The graph's drawn height for a share of the pane, inside its bounds. */
export function seriesGraphHeight(share: number, sizes: SeriesGraphSizes): number {
  const { min, max } = seriesGraphBounds(sizes);
  return Math.round(Math.min(max, Math.max(min, share * sizes.pane)));
}

function readShare(): number {
  try {
    const stored = Number(window.localStorage.getItem(KEY));
    return stored > 0 && stored < 1 ? stored : GRAPH_SHARE_DEFAULT;
  } catch {
    return GRAPH_SHARE_DEFAULT;
  }
}

function keep(share: number) {
  try {
    window.localStorage.setItem(KEY, share.toFixed(3));
  } catch {
    /* kept for this page only */
  }
}

// What the pane holds now: its height, the graph's own height (its children and the gaps between them), the chat's fixed parts.
function measure(pane: HTMLElement): SeriesGraphSizes {
  const overview = pane.querySelector<HTMLElement>(".sr-overview");
  const chat = pane.querySelector<HTMLElement>(".sr-chat");
  const scroll = pane.querySelector<HTMLElement>(".sr-scroll");
  const parts = overview ? Array.from(overview.children) as HTMLElement[] : [];
  const gap = overview ? parseFloat(getComputedStyle(overview).rowGap) || 0 : 0;
  const natural = parts.reduce((sum, part) => sum + part.offsetHeight, 0) + gap * Math.max(0, parts.length - 1);
  const chatFixed = chat ? chat.offsetHeight - (scroll?.offsetHeight ?? 0) : 0;
  return { pane: pane.clientHeight, natural, chatFixed };
}

export interface SeriesGraphSplit {
  /** The graph's drawn height in px; null while the pane is unmeasured (the stylesheet's default holds). */
  height: number | null;
  separator: {
    role: "separator";
    tabIndex: 0;
    "aria-orientation": "horizontal";
    "aria-valuenow": number;
    "aria-valuemin": number;
    "aria-valuemax": number;
    onKeyDown: (event: KeyboardEvent) => void;
    onPointerDown: (event: PointerEvent) => void;
    onPointerMove: (event: PointerEvent) => void;
    onPointerUp: (event: PointerEvent) => void;
  };
}

/** @param content what the pane draws (the series detail): a new one is measured again. */
export function useSeriesGraphSplit(pane: RefObject<HTMLElement | null>, collapsed: boolean, content?: unknown): SeriesGraphSplit {
  const [share, setShare] = useState(GRAPH_SHARE_DEFAULT);
  const [sizes, setSizes] = useState<SeriesGraphSizes>({ pane: 0, natural: 0, chatFixed: 0 });
  const drag = useRef<{ y: number; height: number } | null>(null);
  useEffect(() => setShare(readShare()), []);
  useEffect(() => {
    const element = pane.current;
    if (!element || collapsed || typeof ResizeObserver === "undefined") return;
    const update = () => setSizes(measure(element));
    const observer = new ResizeObserver(update);
    observer.observe(element);
    for (const part of element.querySelectorAll(".sr-overview > *, .sr-chat, .sr-scroll")) observer.observe(part);
    update();
    return () => observer.disconnect();
  }, [pane, collapsed, content]);

  const measured = sizes.pane > 0;
  const bounds = seriesGraphBounds(sizes);
  const height = measured ? seriesGraphHeight(share, sizes) : null;
  const lowest = measured ? bounds.min / sizes.pane : 0.1;
  const highest = measured ? bounds.max / sizes.pane : 0.9;
  // A share outside what the pane allows is drawn at the bound; a move starts from what is drawn.
  const shown = measured && height !== null ? height / sizes.pane : Math.min(highest, Math.max(lowest, share));
  const set = useCallback((next: number, low: number, high: number) => {
    const clamped = Math.min(high, Math.max(low, next));
    setShare(clamped);
    keep(clamped);
  }, []);

  const onKeyDown = (event: KeyboardEvent) => {
    const to = event.key === "ArrowUp" ? shown - STEP : event.key === "ArrowDown" ? shown + STEP
      : event.key === "Home" ? lowest : event.key === "End" ? highest : null;
    if (to === null) return;
    event.preventDefault();
    set(to, lowest, highest);
  };
  const onPointerDown = (event: PointerEvent) => {
    if (!measured || height === null) return;
    drag.current = { y: event.clientY, height };
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
  };
  const onPointerMove = (event: PointerEvent) => {
    if (!drag.current) return;
    setShare(Math.min(highest, Math.max(lowest, (drag.current.height + event.clientY - drag.current.y) / sizes.pane)));
  };
  const onPointerUp = (event: PointerEvent) => {
    if (!drag.current) return;
    drag.current = null;
    (event.currentTarget as Element).releasePointerCapture?.(event.pointerId);
    setShare((now) => { keep(now); return now; });
  };

  return {
    height,
    separator: {
      role: "separator", tabIndex: 0, "aria-orientation": "horizontal",
      "aria-valuenow": Math.round(shown * 100), "aria-valuemin": Math.round(lowest * 100), "aria-valuemax": Math.round(highest * 100),
      onKeyDown, onPointerDown, onPointerMove, onPointerUp,
    },
  };
}
