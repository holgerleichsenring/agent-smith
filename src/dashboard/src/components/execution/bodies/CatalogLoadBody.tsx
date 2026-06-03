"use client";

import { useState } from "react";
import type { RunEvent } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";

// p0205: Load-catalog step body — renders the per-run CatalogLoadedEvent so the
// operator sees exactly what THIS run bound to: the catalog version + source +
// origin URL, the counts line (concepts / skills / masters), and a warm-cache
// vs fresh-pull badge. Empty fallback before the event lands (mid-resolve) or
// for runs where the resolver was bypassed.
//
// p0210: under the header, three collapsed-by-default lists (Skills / Masters /
// Concepts) of the actual names this run bound to; the Concepts list (the big
// one) carries an inline filter. Pre-p0210 runs carry no name arrays — the
// lists are skipped and the counts line stays the at-a-glance summary.

interface CatalogLoadBodyProps {
  events: RunEvent[];
  testId?: string;
}

export function CatalogLoadBody({ events, testId = "catalog-load-body" }: CatalogLoadBodyProps) {
  const latest = pickLatest(events);
  if (!latest) {
    return (
      <div data-testid={testId} className="py-2 text-sm text-stone-400">
        Waiting for catalog binding…
      </div>
    );
  }
  return (
    <div data-testid={testId} className="space-y-2.5 text-sm text-stone-700">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <code
          data-testid={`${testId}-version`}
          className="rounded bg-stone-100 px-1.5 py-0.5 font-mono text-[11px] text-stone-700"
        >
          {latest.version}
        </code>
        <span data-testid={`${testId}-source`} className="text-stone-600">
          {latest.source.toLowerCase()}
        </span>
        <CacheBadge fromCache={latest.fromCache} testId={testId} />
      </div>
      <div data-testid={`${testId}-url`} className="break-all font-mono text-[12px] text-stone-500">
        {latest.sourceUrl}
      </div>
      <div data-testid={`${testId}-counts`} className="font-mono text-[12px] text-stone-600">
        {latest.conceptCount} concepts · {latest.skillsLoaded} skills · {latest.mastersCount} masters
      </div>
      <div data-testid={`${testId}-loaded-at`} className="font-mono text-[11px] text-stone-400">
        loaded {formatTime(latest.timestamp)} · {latest.durationMs}ms
      </div>
      <NameList label="Skills" names={latest.skillNames} testId={`${testId}-skills`} />
      <NameList label="Masters" names={latest.masterNames} testId={`${testId}-masters`} />
      <ConceptList names={latest.conceptNames} testId={`${testId}-concepts`} />
    </div>
  );
}

function NameList({ label, names, testId }: { label: string; names?: string[]; testId: string }) {
  const [open, setOpen] = useState(false);
  if (!names || names.length === 0) return null;
  return (
    <div data-testid={testId} className="border-t border-stone-100 pt-2">
      <ListToggle label={label} count={names.length} open={open} onToggle={() => setOpen((o) => !o)} testId={testId} />
      {open && (
        <ul data-testid={`${testId}-items`} className="mt-1 space-y-0.5 font-mono text-[12px] text-stone-600">
          {names.map((name) => (
            <li key={name}>{name}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

function ConceptList({ names, testId }: { names?: string[]; testId: string }) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  if (!names || names.length === 0) return null;
  const needle = filter.trim().toLowerCase();
  const shown = needle ? names.filter((n) => n.toLowerCase().includes(needle)) : names;
  return (
    <div data-testid={testId} className="border-t border-stone-100 pt-2">
      <ListToggle label="Concepts" count={names.length} open={open} onToggle={() => setOpen((o) => !o)} testId={testId} />
      {open && (
        <div className="mt-1 space-y-1">
          <input
            data-testid={`${testId}-filter`}
            type="text"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            placeholder="filter concepts…"
            className="w-full rounded border border-stone-200 px-2 py-1 text-[12px] text-stone-700 focus:outline-none focus:ring-1 focus:ring-stone-300"
          />
          <ul data-testid={`${testId}-items`} className="space-y-0.5 font-mono text-[12px] text-stone-600">
            {shown.map((name) => (
              <li key={name}>{name}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function ListToggle({
  label,
  count,
  open,
  onToggle,
  testId,
}: {
  label: string;
  count: number;
  open: boolean;
  onToggle: () => void;
  testId: string;
}) {
  return (
    <button
      type="button"
      data-testid={`${testId}-toggle`}
      onClick={onToggle}
      aria-expanded={open}
      className="flex w-full items-center gap-1.5 text-left text-[12px] font-medium text-stone-600 hover:text-stone-800"
    >
      <span className="text-stone-400">{open ? "▾" : "▸"}</span>
      <span>{label}</span>
      <span className="font-mono text-stone-400">{count}</span>
    </button>
  );
}

function formatTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toISOString().slice(11, 19);
}

function CacheBadge({ fromCache, testId }: { fromCache: boolean; testId: string }) {
  return fromCache ? (
    <span
      data-testid={`${testId}-cache`}
      className="rounded-full bg-stone-100 px-2 py-0.5 text-[11px] text-stone-600"
    >
      warm cache
    </span>
  ) : (
    <span
      data-testid={`${testId}-fresh`}
      className="rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] text-emerald-700"
    >
      fresh pull
    </span>
  );
}

function pickLatest(events: RunEvent[]) {
  const candidates = events.filter(
    (e): e is Extract<RunEvent, { type: EventType.CatalogLoaded }> =>
      e.type === EventType.CatalogLoaded,
  );
  if (candidates.length === 0) return null;
  return candidates.reduce((latest, e) =>
    e.timestamp.localeCompare(latest.timestamp) > 0 ? e : latest,
  );
}
