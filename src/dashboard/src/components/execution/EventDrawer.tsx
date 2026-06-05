"use client";

import { useMemo, useState, type ReactNode } from "react";
import { Chip } from "@/components/ui/Chip";

// p0183: typed-event drawer that lives inside an ExecutionNode body.
// Filter chips per event-kind bucket, newest-first sort by default with
// toggle to oldest-first, content search, and a cap-at-N expander so
// hundreds-of-event sub-agents don't push the rest of the tree off screen.

export type EventKind = "obs" | "find" | "tool" | "llm" | "file" | "dec";

export interface DrawerEvent {
  id: string;
  timestamp: string;
  kind: EventKind;
  severity?: "high" | "med" | "info";
  /** Pre-rendered body. Drawer is presentational over EventKind metadata. */
  body: ReactNode;
  /** Plain-text projection of body used by the search filter. */
  searchText: string;
}

interface EventDrawerProps {
  events: DrawerEvent[];
  defaultCap?: number;
}

const KIND_LABEL: Record<EventKind, string> = {
  obs: "obs",
  find: "finding",
  tool: "tool",
  llm: "llm",
  file: "file",
  dec: "decision",
};

const KIND_TAG_COLOR: Record<EventKind, string> = {
  obs: "text-violet-600",
  find: "text-rose-600",
  tool: "text-cyan-600",
  llm: "text-amber-600",
  file: "text-emerald-700",
  dec: "text-stone-900",
};

const FILTER_BUTTONS: Array<{ key: EventKind | "all"; label: string }> = [
  { key: "all", label: "All" },
  { key: "find", label: "Findings" },
  { key: "obs", label: "Obs" },
  { key: "tool", label: "Tools" },
  { key: "llm", label: "LLM" },
  { key: "file", label: "Files" },
  { key: "dec", label: "Decisions" },
];

export function EventDrawer({ events, defaultCap = 8 }: EventDrawerProps) {
  const [active, setActive] = useState<Set<EventKind | "all">>(new Set(["all"]));
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<"new" | "old">("new");
  const [showAll, setShowAll] = useState(false);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    let list = events.slice();
    if (!active.has("all")) {
      list = list.filter((e) => active.has(e.kind));
    }
    if (q) {
      list = list.filter((e) => e.searchText.toLowerCase().includes(q));
    }
    list.sort((a, b) =>
      sort === "new" ? b.timestamp.localeCompare(a.timestamp) : a.timestamp.localeCompare(b.timestamp),
    );
    return list;
  }, [events, active, query, sort]);

  const shown = showAll ? filtered : filtered.slice(0, defaultCap);
  const overCap = filtered.length > defaultCap;

  function toggleChip(k: EventKind | "all") {
    setActive((prev) => {
      const next = new Set(prev);
      if (k === "all") {
        return new Set(["all"]);
      }
      next.delete("all");
      if (next.has(k)) {
        next.delete(k);
      } else {
        next.add(k);
      }
      if (next.size === 0) {
        next.add("all");
      }
      return next;
    });
  }

  return (
    <div data-testid="event-drawer">
      <div className="flex flex-wrap items-center gap-1.5 py-2">
        {FILTER_BUTTONS.map(({ key, label }) => (
          <Chip
            key={key}
            testId={`event-drawer-chip-${key}`}
            label={label}
            selected={active.has(key)}
            onClick={() => toggleChip(key)}
          />
        ))}
        <button
          type="button"
          data-testid="event-drawer-sort"
          onClick={() => setSort((s) => (s === "new" ? "old" : "new"))}
          className="rounded-full border border-stone-200 bg-white px-2 py-1 dsh-label text-stone-500"
        >
          {sort === "new" ? "newest ↓" : "oldest ↑"}
        </button>
        <span className="ml-auto">
          <input
            data-testid="event-drawer-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="filter content…"
            className="w-40 rounded-full border border-stone-200 px-3 py-1 dsh-mono text-stone-700 outline-none focus:border-emerald-500"
          />
        </span>
      </div>
      <div className="mt-1" data-testid="event-drawer-list">
        {shown.length === 0 ? (
          <div className="py-3 text-center dsh-mono text-stone-400">No matching events.</div>
        ) : (
          shown.map((e) => (
            <div
              key={e.id}
              data-testid={`event-drawer-row-${e.id}`}
              className="flex gap-2.5 border-b border-stone-100 py-1.5 text-sm last:border-b-0"
            >
              <span className="w-14 flex-none pt-px font-mono dsh-mono text-stone-400">
                {e.timestamp}
              </span>
              <span
                className={`w-16 flex-none pt-0.5 font-mono dsh-mono font-semibold uppercase ${KIND_TAG_COLOR[e.kind]}`}
              >
                {KIND_LABEL[e.kind]}
              </span>
              <span className="flex-1 text-stone-600">{e.body}</span>
            </div>
          ))
        )}
      </div>
      {overCap && (
        <button
          type="button"
          data-testid="event-drawer-show-all"
          onClick={() => setShowAll((s) => !s)}
          className="mt-1 inline-flex items-center gap-1.5 py-1 dsh-mono font-medium text-emerald-700 hover:underline"
        >
          {showAll ? "▴ show fewer" : `▾ show all ${filtered.length} events`}
        </button>
      )}
    </div>
  );
}
