"use client";

import type { RunWorkKind } from "@/lib/runStoryApi";
import { formatMs } from "./callSeries";

// p0341h: what the run spent its time ON. The panel this sits above answered "how much"
// and never "on what", and one of its numbers was a lie by construction — it counted an
// event the trail never receives, so a run that issued 444 sandbox commands reported zero.
//
// Bars are proportional to TIME, not to count: twelve builds outweigh a hundred greps, and
// that ordering is the finding. The rows arrive already sorted by duration.

export interface WorkBreakdownProps {
  title: string;
  subtitle: string;
  kinds: RunWorkKind[];
  testId: string;
}

export function WorkBreakdown({ title, subtitle, kinds, testId }: WorkBreakdownProps) {
  if (kinds.length === 0) return null;
  const total = kinds.reduce((sum, k) => sum + k.durationMs, 0);
  const longest = Math.max(...kinds.map((k) => k.durationMs), 1);
  const runs = kinds.reduce((sum, k) => sum + k.count, 0);

  return (
    <div className="mb-5" data-testid={testId}>
      <div className="mb-2 flex items-baseline justify-between gap-3">
        <h4 className="text-xs font-medium uppercase tracking-wide text-stone-500">{title}</h4>
        <span className="text-xs text-stone-500">
          {runs} {subtitle} · {formatMs(total)}
        </span>
      </div>
      <ul className="flex flex-col gap-1">
        {kinds.map((k) => (
          <Row key={k.label} kind={k} longest={longest} />
        ))}
      </ul>
    </div>
  );
}

function Row({ kind, longest }: { kind: RunWorkKind; longest: number }) {
  // The bar is the only thing a reader scans before deciding where to look, so it carries
  // the number that decides that: wall clock, relative to the heaviest row.
  const width = Math.max(2, Math.round((kind.durationMs / longest) * 100));
  return (
    <li className="flex items-center gap-3 text-sm" data-testid={`work-row-${kind.label}`}>
      <span className="w-52 shrink-0 truncate text-stone-700" title={kind.label}>
        {kind.label}
      </span>
      <span className="w-10 shrink-0 text-right tabular-nums text-stone-500">
        {kind.count}&times;
      </span>
      <span className="h-2 min-w-0 flex-1 overflow-hidden rounded-sm bg-stone-100">
        <span
          className={`block h-full rounded-sm ${kind.failed > 0 ? "bg-red-300" : "bg-stone-400"}`}
          style={{ width: `${width}%` }}
        />
      </span>
      <span className="w-16 shrink-0 text-right tabular-nums text-stone-600">
        {formatMs(kind.durationMs)}
      </span>
      <span className="w-20 shrink-0 text-right text-xs text-red-600">
        {kind.failed > 0 ? `${kind.failed} failed` : ""}
      </span>
    </li>
  );
}
