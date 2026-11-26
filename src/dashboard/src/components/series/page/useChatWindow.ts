"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type RefObject } from "react";

// 2026-11-26-5201a: the series chat as a chat window. The transcript comes whole in one read (the dialog read has no
// paging), so the window is the browser's: it renders the newest entries, opens at the bottom, stays pinned there while
// the reader is there, never moves while they scrolled up, and renders older entries as they scroll toward the top.

/** How many entries one window step renders. A render size, not a limit: a long conversation (twenty printed pages)
 *  rendered whole draws hundreds of turns and reads every agent turn's steps at once; thirty fill a tall screen twice. */
export const CHAT_PAGE = 30;

/** How close to the box's top a scroll loads older entries, and how close to its bottom still counts as at the bottom. */
const NEAR_TOP_PX = 80;
const AT_BOTTOM_PX = 24;

export interface ChatWindow {
  box: RefObject<HTMLDivElement | null>;
  /** The box's one inner element; its height changes are what keep a reader at the bottom there. */
  content: RefObject<HTMLDivElement | null>;
  /** The index of the first entry shown. */
  from: number;
  onScroll: () => void;
  /** Renders CHAT_PAGE older entries, keeping the reader's place. */
  more: () => void;
}

export function useChatWindow(count: number): ChatWindow {
  const box = useRef<HTMLDivElement | null>(null);
  const content = useRef<HTMLDivElement | null>(null);
  const [anchor, setAnchor] = useState<number | null>(null);
  const atBottom = useRef(true);
  const restore = useRef<{ height: number; top: number } | null>(null);
  // The window is fixed by its first entry once there are entries, so an arriving entry appends and none drops out above.
  const from = Math.min(anchor ?? Math.max(0, count - CHAT_PAGE), count);
  // A transcript that was cleared or replaced by a shorter one starts over at its newest entries.
  useLayoutEffect(() => {
    if (count === 0 || (anchor !== null && anchor >= count)) {
      if (anchor !== null) { setAnchor(null); atBottom.current = true; }
    } else if (anchor === null) setAnchor(Math.max(0, count - CHAT_PAGE));
  }, [anchor, count]);

  const pin = useCallback(() => {
    const el = box.current;
    if (!el) return;
    if (restore.current) {
      el.scrollTop = restore.current.top + (el.scrollHeight - restore.current.height);
      restore.current = null;
    } else if (atBottom.current) {
      el.scrollTop = el.scrollHeight;
    }
  }, []);

  useLayoutEffect(pin, [count, from, pin]);

  // A turn's steps, an image or the working line arrive after the entry rendered; the content grows, and a reader at the
  // bottom stays there.
  useEffect(() => {
    const el = box.current;
    const inner = content.current;
    if (!el || !inner || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(() => { if (atBottom.current && !restore.current) el.scrollTop = el.scrollHeight; });
    observer.observe(inner);
    return () => observer.disconnect();
  }, []);

  const more = useCallback(() => {
    const el = box.current;
    if (from === 0) return;
    if (el) restore.current = { height: el.scrollHeight, top: el.scrollTop };
    atBottom.current = false;
    setAnchor(Math.max(0, from - CHAT_PAGE));
  }, [from]);

  const onScroll = useCallback(() => {
    const el = box.current;
    if (!el) return;
    atBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight <= AT_BOTTOM_PX;
    if (el.scrollTop <= NEAR_TOP_PX && from > 0 && !restore.current) more();
  }, [from, more]);

  return { box, content, from, onScroll, more };
}
