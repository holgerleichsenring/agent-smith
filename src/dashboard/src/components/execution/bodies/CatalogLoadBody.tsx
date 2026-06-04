"use client";

import Link from "next/link";
import type { RunEvent } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";

// p0205: Load-catalog step body — renders the per-run CatalogLoadedEvent so the
// operator sees exactly what THIS run bound to: the catalog version + source +
// origin URL, the counts line (concepts / skills / masters), and a warm-cache
// vs fresh-pull badge. Empty fallback before the event lands (mid-resolve) or
// for runs where the resolver was bypassed.
//
// p0221: the per-run step keeps the lightweight version/source/counts summary
// and LINKS to the System catalog browser (/system/catalog) rather than
// duplicating the skill/master/concept contents per run. The full contents
// (rendered SKILL.md bodies, concept type + definition) live on the system
// page, served by the lazy catalog-contents endpoint.

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
          className="rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-label text-stone-700"
        >
          {latest.version}
        </code>
        <span data-testid={`${testId}-source`} className="text-stone-600">
          {latest.source.toLowerCase()}
        </span>
        <CacheBadge fromCache={latest.fromCache} testId={testId} />
      </div>
      <div data-testid={`${testId}-url`} className="break-all font-mono dsh-mono text-stone-500">
        {latest.sourceUrl}
      </div>
      <div data-testid={`${testId}-counts`} className="font-mono dsh-mono text-stone-600">
        {latest.conceptCount} concepts · {latest.skillsLoaded} skills · {latest.mastersCount} masters
      </div>
      <div data-testid={`${testId}-loaded-at`} className="font-mono dsh-label text-stone-400">
        loaded {formatTime(latest.timestamp)} · {latest.durationMs}ms
      </div>
      <Link
        href="/system/catalog"
        data-testid={`${testId}-browse`}
        className="inline-block border-t border-stone-100 pt-2 dsh-mono font-medium text-emerald-700 hover:underline"
      >
        Browse the full catalog →
      </Link>
    </div>
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
      className="rounded-full bg-stone-100 px-2 py-0.5 dsh-label text-stone-600"
    >
      warm cache
    </span>
  ) : (
    <span
      data-testid={`${testId}-fresh`}
      className="rounded-full bg-emerald-50 px-2 py-0.5 dsh-label text-emerald-700"
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
