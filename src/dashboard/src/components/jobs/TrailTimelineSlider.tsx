"use client";

import { useEffect, useRef, useState } from "react";

interface Props {
  startMs: number;
  endMs: number;
  value: number;
  onChange: (value: number) => void;
}

export function TrailTimelineSlider({ startMs, endMs, value, onChange }: Props) {
  const [playing, setPlaying] = useState(false);
  const rafRef = useRef<number | null>(null);
  const lastTickRef = useRef<number>(0);
  const range = Math.max(1, endMs - startMs);

  useEffect(() => {
    if (!playing) {
      if (rafRef.current !== null) cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
      return;
    }
    const replayScale = 6; // 2 min real → 20s replay
    const tick = (now: number) => {
      if (lastTickRef.current === 0) lastTickRef.current = now;
      const deltaReal = now - lastTickRef.current;
      lastTickRef.current = now;
      const next = Math.min(endMs, value + deltaReal * replayScale);
      onChange(next);
      if (next >= endMs) {
        setPlaying(false);
        lastTickRef.current = 0;
        return;
      }
      rafRef.current = requestAnimationFrame(tick);
    };
    rafRef.current = requestAnimationFrame(tick);
    return () => {
      if (rafRef.current !== null) cancelAnimationFrame(rafRef.current);
    };
  }, [playing, value, endMs, onChange]);

  return (
    <div className="flex items-center gap-3 rounded-lg border border-stone-200 bg-white p-3" data-testid="trail-timeline">
      <button
        type="button"
        onClick={() => {
          lastTickRef.current = 0;
          setPlaying((p) => !p);
        }}
        className="rounded bg-stone-800 px-2 py-1 text-xs text-white hover:bg-stone-700"
        data-testid="timeline-play-button"
      >
        {playing ? "pause" : "play"}
      </button>
      <input
        type="range"
        min={startMs}
        max={endMs}
        step={Math.max(1, Math.floor(range / 100))}
        value={value}
        onChange={(e) => {
          setPlaying(false);
          onChange(Number(e.target.value));
        }}
        className="flex-1"
        data-testid="timeline-slider"
      />
      <span className="font-mono text-xs text-stone-500">
        {Math.round((value - startMs) / 1000)}s
      </span>
    </div>
  );
}
