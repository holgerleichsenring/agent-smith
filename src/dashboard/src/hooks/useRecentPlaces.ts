"use client";

import { useEffect, useState } from "react";
import { shortRunId } from "@/lib/runId";

// 2026-11-16-0440c: the sidebar's Recent group — the last ticket and run pages THIS browser
// opened, newest first. It is a per-viewer convenience, so it lives in localStorage and every
// read and write survives storage that is blocked or absent: the list is then simply empty.

export interface RecentPlace {
  href: string;
  label: string;
  kind: "ticket" | "run";
}

const KEY = "agentsmith.recent-places";
const LIMIT = 5;

/** The recent entry a path is, or null for a page that is not a ticket or a run. */
export function recentPlaceFor(pathname: string): RecentPlace | null {
  const segments = pathname.split("?")[0].split("/").filter((s) => s.length > 0);
  if (segments[0] === "tickets" && segments.length === 3) {
    const [project, ticket] = [decodeURIComponent(segments[1]), decodeURIComponent(segments[2])];
    return { href: `/tickets/${segments[1]}/${segments[2]}`, label: `${ticket} · ${project}`, kind: "ticket" };
  }
  if (segments[0] === "jobs" && segments.length === 2) {
    return { href: `/jobs/${segments[1]}`, label: `Run ${shortRunId(decodeURIComponent(segments[1]))}`, kind: "run" };
  }
  return null;
}

/** `place` first, any earlier visit to it dropped, at most LIMIT entries. */
export function remember(list: readonly RecentPlace[], place: RecentPlace): RecentPlace[] {
  return [place, ...list.filter((p) => p.href !== place.href)].slice(0, LIMIT);
}

function read(): RecentPlace[] {
  try {
    const parsed: unknown = JSON.parse(window.localStorage.getItem(KEY) ?? "[]");
    return Array.isArray(parsed)
      ? parsed.filter((p): p is RecentPlace => typeof p?.href === "string" && typeof p?.label === "string").slice(0, LIMIT)
      : [];
  } catch {
    return [];
  }
}

function write(list: RecentPlace[]): void {
  try {
    window.localStorage.setItem(KEY, JSON.stringify(list));
  } catch {
    /* blocked storage: the list lives for this page only */
  }
}

export function useRecentPlaces(pathname: string): RecentPlace[] {
  const [recent, setRecent] = useState<RecentPlace[]>([]);
  useEffect(() => {
    const place = recentPlaceFor(pathname);
    const next = place ? remember(read(), place) : read();
    if (place) write(next);
    setRecent(next);
  }, [pathname]);
  return recent;
}
