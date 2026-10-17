"use client";

import { useCallback, useEffect, useRef, useState, type RefObject } from "react";
import { scrollBehavior } from "@/lib/scrollBehavior";
import type { TicketConversationEntry } from "@/types/tickets";

// 2026-10-17-e730f: whether the reader is away from the end of the conversation, and how many entries arrived since
// they were last there. "At the end" compares the list's end (the sentinel) with the docked composer's top, not
// with the viewport: the dock covers the bottom of the scroll box. Every entry on the first render that has the
// conversation counts as seen, so opening the tab elsewhere than the end (a question, a ?causeOf= entry) counts
// nothing; arrivals come as whole re-reads, so one set of ids counts them.

/** The sentinel may sit this far below the dock's top and still count as at the end. */
const SLACK_PX = 48;

export interface NewestJump {
  away: boolean;
  /** Entries whose id was not on the page when the reader was last at the end (or when the tab opened). */
  count: number;
  jump: () => void;
}

export function useNewestJump(
  entries: TicketConversationEntry[] | null,
  sentinel: RefObject<HTMLElement | null>,
  dock: RefObject<HTMLElement | null>,
  end: RefObject<HTMLElement | null>,
): NewestJump {
  const seen = useRef<Set<string> | null>(null);
  if (entries && seen.current === null) seen.current = new Set(entries.map((e) => e.id));
  const [away, setAway] = useState(false);

  const check = useCallback(() => {
    const list = sentinel.current;
    const composer = dock.current;
    if (!list || !composer) return;
    const atEnd = list.getBoundingClientRect().top <= composer.getBoundingClientRect().top + SLACK_PX;
    if (atEnd) for (const e of entries ?? []) seen.current?.add(e.id);
    setAway(!atEnd);
  }, [entries, sentinel, dock]);

  useEffect(() => {
    check();
    // Scroll events do not bubble; a capturing listener on the document hears the layout's scroll box too.
    document.addEventListener("scroll", check, { capture: true, passive: true });
    window.addEventListener("resize", check);
    return () => {
      document.removeEventListener("scroll", check, { capture: true });
      window.removeEventListener("resize", check);
    };
  }, [check]);

  const known = seen.current;
  const count = entries && known ? entries.filter((e) => !known.has(e.id)).length : 0;
  const jump = () => end.current?.scrollIntoView({ block: "end", behavior: scrollBehavior() });
  return { away, count, jump };
}
