"use client";

import { yourMoveCount } from "@/components/tickets/holds/holdWords";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";
import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bell, Search } from "lucide-react";
import { PLACE_KEYS, PLACES } from "@/lib/places";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { ticketBucketHref } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";

// 2026-11-16-0440c: the bar above every page — one search field and the bell. The field goes to
// a page by name: its suggestions are the places table the shell already keeps, and ⌘K (Ctrl K)
// puts the cursor in it. Searching tickets and runs is a later spec. The bell has no feed behind
// it yet; it opens what waits on you and carries a dot while anything does.

type Place = { name: string; path: string };

function unique(entries: Array<[string, string]>): Place[] {
  return entries
    .filter(([, name], i, all) => all.findIndex(([, other]) => other === name) === i)
    .map(([path, name]) => ({ name, path }));
}

/** Every page the field can go to, one path per name. */
export const SEARCHABLE_PLACES: ReadonlyArray<Place> = unique(Object.entries(PLACES));

/** 2026-11-23-5669a: the same pages named in the chosen language; a page the dictionary does not name keeps its name. */
export function searchablePlaces(t: T): ReadonlyArray<Place> {
  return unique(Object.entries(PLACES).map(([path, name]) => [path, PLACE_KEYS[path] ? t(PLACE_KEYS[path]) : name]));
}

/** The page a typed text names: an exact name first, else the first name starting with it; the English names always match. */
export function placeForQuery(query: string, places: ReadonlyArray<Place> = SEARCHABLE_PLACES): string | null {
  const wanted = query.trim().toLowerCase();
  if (wanted.length === 0) return null;
  for (const list of [places, SEARCHABLE_PLACES]) {
    const exact = list.find((p) => p.name.toLowerCase() === wanted);
    const found = exact ?? list.find((p) => p.name.toLowerCase().startsWith(wanted));
    if (found) return found.path;
  }
  return null;
}

export function AppTopBar() {
  const router = useRouter();
  const t = useT();
  const places = searchablePlaces(t);
  const field = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const needsYou = yourMoveCount(useTicketList().list?.counts, useHoldsAdmin()) ?? 0; // 2026-11-23-31dcg

  useEffect(() => {
    const focus = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        field.current?.focus();
      }
    };
    window.addEventListener("keydown", focus);
    return () => window.removeEventListener("keydown", focus);
  }, []);

  const go = (event: React.FormEvent) => {
    event.preventDefault();
    const path = placeForQuery(query, places);
    if (!path) return;
    setQuery("");
    field.current?.blur();
    router.push(path);
  };

  return (
    <header className="app-topbar" data-testid="app-topbar">
      <form role="search" className="tb-search" onSubmit={go}>
        <Search size={15} strokeWidth={2} aria-hidden="true" />
        <input
          ref={field}
          type="search"
          list="tb-places"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={t("shell.search.placeholder")}
          aria-label={t("shell.search.placeholder")}
          data-testid="topbar-search"
        />
        <kbd aria-hidden="true">⌘K</kbd>
        <datalist id="tb-places">
          {places.map((p) => (
            <option key={p.path} value={p.name} />
          ))}
        </datalist>
      </form>
      <Link
        href={ticketBucketHref("needs-you")}
        className="tb-bell"
        aria-label={needsYou > 0 ? t("shell.bell.waiting", { count: needsYou }) : t("shell.bell.nothing")}
        title={needsYou > 0 ? t("shell.bell.waiting", { count: needsYou }) : t("shell.bell.nothing")}
        data-testid="topbar-bell"
      >
        <Bell size={17} strokeWidth={1.9} aria-hidden="true" />
        {needsYou > 0 && <span className="tb-bell-dot" data-testid="topbar-bell-dot" />}
      </Link>
    </header>
  );
}
