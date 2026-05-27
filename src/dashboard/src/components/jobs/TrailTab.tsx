"use client";

import { useMemo, useState } from "react";
import { useTrail } from "@/hooks/useTrail";
import { assembleTrail } from "@/lib/TrailAssembler";
import type { TrailNode } from "@/types/trail-node";
import { TrailTree } from "./TrailTree";
import { TrailNodeDetailPane } from "./TrailNodeDetailPane";
import { TrailTimelineSlider } from "./TrailTimelineSlider";
import { TrailTruncationBanner } from "./TrailTruncationBanner";
import { TrailExpiredEmptyState } from "./TrailExpiredEmptyState";

interface Props {
  runId: string;
  isFinished: boolean;
  prUrl: string | null;
}

export function TrailTab({ runId, isFinished, prUrl }: Props) {
  const { events, loading, error } = useTrail(runId);
  const assembled = useMemo(() => assembleTrail(events), [events]);
  const [selected, setSelected] = useState<TrailNode | null>(null);
  const [scrubMs, setScrubMs] = useState<number | null>(null);

  const root = assembled.root;
  const startMs = root?.startedAtMs ?? 0;
  const endMs = useMemo(
    () => root ? computeEndMs(events, root) : 0,
    [events, root],
  );
  const effectiveScrub = scrubMs ?? endMs;
  const filtered = useMemo(
    () => root ? cloneFiltered(root, effectiveScrub) : null,
    [root, effectiveScrub],
  );

  if (loading) return <p className="text-sm text-stone-500" data-testid="trail-loading">Loading trail…</p>;
  if (error) return <p className="text-sm text-rose-700" data-testid="trail-error">{error}</p>;
  if (!root || !filtered) {
    if (isFinished) return <TrailExpiredEmptyState prUrl={prUrl} />;
    return <p className="text-sm text-stone-500" data-testid="trail-empty">No trail events yet.</p>;
  }

  return (
    <div className="space-y-3" data-testid="trail-tab">
      {assembled.truncated && <TrailTruncationBanner />}
      <TrailTimelineSlider
        startMs={startMs}
        endMs={endMs}
        value={effectiveScrub}
        onChange={setScrubMs}
      />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,35fr)_minmax(0,65fr)]">
        <TrailTree root={filtered} selectedId={selected?.id ?? null} onSelect={setSelected} />
        <TrailNodeDetailPane node={selected} />
      </div>
    </div>
  );
}

function computeEndMs(events: { timestamp: string }[], root: TrailNode): number {
  if (events.length === 0) return root.startedAtMs + 1;
  return Date.parse(events[events.length - 1].timestamp);
}

function cloneFiltered(node: TrailNode, scrubMs: number): TrailNode {
  const children = node.children
    .filter((c) => c.startedAtMs <= scrubMs)
    .map((c) => cloneFiltered(c, scrubMs));
  return { ...node, children };
}
