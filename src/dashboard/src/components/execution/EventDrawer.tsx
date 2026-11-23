"use client";

import { useMemo, useState, type ReactNode } from "react";
import { Chip } from "@/components/ui/Chip";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

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

const KIND_LABEL: Record<EventKind, TextKey> = {
  obs: "execution.kind.obs",
  find: "execution.kind.find",
  tool: "execution.kind.tool",
  llm: "execution.kind.llm",
  file: "execution.kind.file",
  dec: "execution.kind.dec",
};

const KIND_TAG_COLOR: Record<EventKind, string> = {
  obs: "text-violet-600",
  find: "text-rose-600",
  tool: "text-cyan-600",
  llm: "text-amber-600",
  file: "text-emerald-700",
  dec: "text-stone-900",
};

const FILTER_BUTTONS: Array<{ key: EventKind | "all"; label: TextKey }> = [
  { key: "all", label: "execution.filter.all" },
  { key: "find", label: "execution.filter.find" },
  { key: "obs", label: "execution.filter.obs" },
  { key: "tool", label: "execution.filter.tool" },
  { key: "llm", label: "execution.filter.llm" },
  { key: "file", label: "execution.filter.file" },
  { key: "dec", label: "execution.filter.dec" },
];

export function EventDrawer({ events, defaultCap = 8 }: EventDrawerProps) {
  const t = useT();
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
            label={t(label)}
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
          {sort === "new" ? t("execution.sort.newest") : t("execution.sort.oldest")}
        </button>
        <span className="ml-auto">
          <input
            data-testid="event-drawer-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("execution.filterPlaceholder")}
            className="w-40 rounded-full border border-stone-200 px-3 py-1 dsh-mono text-stone-700 outline-none focus:border-emerald-500"
          />
        </span>
      </div>
      <div className="mt-1" data-testid="event-drawer-list">
        {shown.length === 0 ? (
          <div className="py-3 text-center dsh-mono text-stone-400">{t("execution.noMatch")}</div>
        ) : (
          shown.map((e) => (
            <div
              key={e.id}
              data-testid={`event-drawer-row-${e.id}`}
              className="flex gap-3 border-b border-stone-100 py-2 dsh-h3 last:border-b-0"
            >
              {/* p0264: w-20 — "HH:MM:SS" in mono overflowed the old w-14 box into the
                  gap, jamming the timestamp against the kind tag ("10:08:48DECISION").
                  Sizes bumped one dsh rung (mono 12→dsh-body 14, body 14→dsh-h3 16) —
                  the dense stream read too small. dsh-* tokens, not raw px (p0217). */}
              <span className="w-20 flex-none pt-0.5 font-mono dsh-body text-stone-400">
                {e.timestamp}
              </span>
              <span
                className={`w-20 flex-none pt-0.5 font-mono dsh-body font-semibold uppercase ${KIND_TAG_COLOR[e.kind]}`}
              >
                {t(KIND_LABEL[e.kind])}
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
          {showAll ? t("execution.showFewer") : t("execution.showAll", { count: filtered.length })}
        </button>
      )}
    </div>
  );
}
