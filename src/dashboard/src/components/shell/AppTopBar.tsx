"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bell, Search } from "lucide-react";
import { PLACES } from "@/lib/places";
import { ticketBucketHref } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";

// 2026-11-16-0440c: the bar above every page — one search field and the bell. The field goes to
// a page by name: its suggestions are the places table the shell already keeps, and ⌘K (Ctrl K)
// puts the cursor in it. Searching tickets and runs is a later spec. The bell has no feed behind
// it yet; it opens what waits on you and carries a dot while anything does.

/** Every page the field can go to, one path per name. */
export const SEARCHABLE_PLACES: ReadonlyArray<{ name: string; path: string }> = Object.entries(PLACES)
  .filter(([, name], i, all) => all.findIndex(([, other]) => other === name) === i)
  .map(([path, name]) => ({ name, path }));

/** The page a typed text names: an exact name first, else the first name starting with it. */
export function placeForQuery(query: string): string | null {
  const wanted = query.trim().toLowerCase();
  if (wanted.length === 0) return null;
  const exact = SEARCHABLE_PLACES.find((p) => p.name.toLowerCase() === wanted);
  return (exact ?? SEARCHABLE_PLACES.find((p) => p.name.toLowerCase().startsWith(wanted)))?.path ?? null;
}

export function AppTopBar() {
  const router = useRouter();
  const field = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const needsYou = useTicketList().list?.counts.needsYou ?? 0;

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
    const path = placeForQuery(query);
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
          placeholder="Go to a page"
          aria-label="Go to a page"
          data-testid="topbar-search"
        />
        <kbd aria-hidden="true">⌘K</kbd>
        <datalist id="tb-places">
          {SEARCHABLE_PLACES.map((p) => (
            <option key={p.path} value={p.name} />
          ))}
        </datalist>
      </form>
      <Link
        href={ticketBucketHref("needs-you")}
        className="tb-bell"
        aria-label={needsYou > 0 ? `${needsYou} waiting on you` : "Nothing waits on you"}
        title={needsYou > 0 ? `${needsYou} waiting on you` : "Nothing waits on you"}
        data-testid="topbar-bell"
      >
        <Bell size={17} strokeWidth={1.9} aria-hidden="true" />
        {needsYou > 0 && <span className="tb-bell-dot" data-testid="topbar-bell-dot" />}
      </Link>
    </header>
  );
}
