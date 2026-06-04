"use client";

// p0183: one-line "↳ latest activity" preview shown under a node row when
// not expanded. Lets the operator scan vertically and see "what just
// happened" without clicking each node open.

interface LiveTailProps {
  text: string;
  timestamp: string;
  indentPx?: number;
}

export function LiveTail({ text, timestamp, indentPx = 56 }: LiveTailProps) {
  return (
    <div
      className="flex items-center gap-2 pb-2 pr-3.5"
      style={{ paddingLeft: indentPx }}
      data-testid="live-tail"
    >
      <span className="font-mono dsh-label text-stone-400" aria-hidden="true">
        ↳
      </span>
      <span className="flex-1 truncate font-mono dsh-mono text-stone-600">
        {text}
      </span>
      <span className="font-mono dsh-label text-stone-400">{timestamp}</span>
    </div>
  );
}
