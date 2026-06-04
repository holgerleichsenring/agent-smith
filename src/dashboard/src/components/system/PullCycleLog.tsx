"use client";

import { useCallback, useState } from "react";
import {
  SKIP_REASON_LABEL,
  usePullCycleLog,
  type PullCycleEntry,
} from "@/hooks/usePullCycleLog";
import { TicketSkipReason, type SystemEvent } from "@/types/system-events";

interface Props {
  events: readonly SystemEvent[];
  limit?: number;
}

export function PullCycleLog({ events, limit = 50 }: Props) {
  const cycles = usePullCycleLog(events, limit);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const toggle = useCallback((key: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }, []);

  return (
    <section
      className="rounded-md border border-stone-200 bg-white p-4"
      data-testid="pull-cycle-log"
    >
      <header className="mb-3 flex items-baseline justify-between">
        <h2 className="text-sm font-medium text-stone-700">Pull cycles</h2>
        <span className="text-xs text-stone-500">{cycles.length} most recent</span>
      </header>
      {cycles.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="pull-cycle-empty">
          No pull cycles observed yet.
        </p>
      ) : (
        <ul className="space-y-1.5">
          {cycles.map((cycle) => {
            const key = `${cycle.source}-${cycle.startedAt}`;
            return (
              <li key={key}>
                <CycleRow
                  cycle={cycle}
                  expanded={expanded.has(key)}
                  onToggle={() => toggle(key)}
                />
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function CycleRow({
  cycle,
  expanded,
  onToggle,
}: {
  cycle: PullCycleEntry;
  expanded: boolean;
  onToggle: () => void;
}) {
  const inFlight = cycle.finishedAt === null;
  const accent = inFlight
    ? "border-amber-200 bg-amber-50"
    : cycle.triggered > 0
      ? "border-emerald-200 bg-emerald-50"
      : "border-stone-200 bg-stone-50";
  return (
    <div
      className={`rounded border ${accent}`}
      data-testid={`pull-cycle-${cycle.source}`}
    >
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm"
        aria-expanded={expanded}
      >
        <span className="flex items-center gap-2">
          <span className="rounded bg-stone-800 px-1.5 py-0.5 font-mono dsh-label uppercase tracking-wide text-stone-50">
            pull
          </span>
          <span className="font-medium text-stone-800">{cycle.tracker}</span>
          <span className="font-mono text-xs text-stone-500">{cycle.source}</span>
        </span>
        <span className="flex items-center gap-3 text-xs">
          <DurationBadge durationMs={cycle.durationMs} />
          <CountChip label="scanned" count={cycle.ticketsPolled} tone="neutral" />
          {cycle.triggered > 0 && (
            <CountChip label="triggered" count={cycle.triggered} tone="ok" />
          )}
          {cycle.skippedTotal > 0 && (
            <SkippedHistogram skipped={cycle.skippedByReason} total={cycle.skippedTotal} />
          )}
          <span className="text-stone-400" aria-hidden>
            {expanded ? "▾" : "▸"}
          </span>
        </span>
      </button>
      {expanded ? (
        <div className="border-t border-stone-200 px-3 py-2" data-testid="pull-cycle-detail">
          <p className="mb-1 dsh-label uppercase tracking-wide text-stone-500">
            started {new Date(cycle.startedAt).toLocaleString()}
            {cycle.finishedAt && ` · finished ${new Date(cycle.finishedAt).toLocaleTimeString()}`}
          </p>
          {cycle.triggeredEntries.length > 0 && (
            <div className="mb-1">
              <p className="text-xs text-emerald-700">Triggered:</p>
              <ul className="ml-3 list-disc text-xs text-stone-700">
                {cycle.triggeredEntries.map((t) => (
                  <li key={t.ticketId}>
                    {t.ticketId} → {t.project}/{t.pipeline} ({t.outcome})
                  </li>
                ))}
              </ul>
            </div>
          )}
          {cycle.skippedEntries.length > 0 && (
            <div>
              <p className="text-xs text-stone-600">Skipped:</p>
              <ul className="ml-3 list-disc text-xs text-stone-600">
                {cycle.skippedEntries.slice(0, 20).map((s, idx) => (
                  <li key={`${s.ticketId}-${idx}`}>
                    <span className="font-mono">{s.ticketId}</span>{" "}
                    <span className="text-stone-500">— {SKIP_REASON_LABEL[s.reason]}: {s.detail}</span>
                  </li>
                ))}
                {cycle.skippedEntries.length > 20 && (
                  <li className="text-stone-400">… {cycle.skippedEntries.length - 20} more</li>
                )}
              </ul>
            </div>
          )}
        </div>
      ) : null}
    </div>
  );
}

function DurationBadge({ durationMs }: { durationMs: number | null }) {
  if (durationMs === null) {
    return (
      <span className="text-amber-700" data-testid="duration-in-flight">in-flight…</span>
    );
  }
  const seconds = (durationMs / 1000).toFixed(1);
  return (
    <span className="tabular-nums text-stone-500" data-testid="duration-finished">
      {seconds}s
    </span>
  );
}

function CountChip({
  label,
  count,
  tone,
}: {
  label: string;
  count: number;
  tone: "neutral" | "ok";
}) {
  const cls =
    tone === "ok"
      ? "bg-emerald-100 text-emerald-700"
      : "bg-stone-100 text-stone-600";
  return (
    <span className={`rounded px-1.5 py-0.5 text-xs ${cls}`}>
      {count} {label}
    </span>
  );
}

function SkippedHistogram({
  skipped,
  total,
}: {
  skipped: Record<TicketSkipReason, number>;
  total: number;
}) {
  const reasons = (Object.entries(skipped) as [string, number][])
    .filter(([, c]) => c > 0)
    .sort((a, b) => b[1] - a[1])
    .slice(0, 3);
  return (
    <span className="flex items-center gap-1 text-xs">
      <span className="rounded bg-stone-100 px-1.5 py-0.5 text-stone-600">
        {total} skipped
      </span>
      {reasons.map(([reason, count]) => (
        <span
          key={reason}
          className="rounded bg-stone-50 px-1.5 py-0.5 dsh-label text-stone-500"
          title={SKIP_REASON_LABEL[Number(reason) as TicketSkipReason]}
        >
          {count}× {SKIP_REASON_LABEL[Number(reason) as TicketSkipReason]}
        </span>
      ))}
    </span>
  );
}
