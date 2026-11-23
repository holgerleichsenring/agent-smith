"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import { isPathLike, shortPath, splitList } from "./logPaths";

// 2026-11-18-e06bc: what a criterion cites, short. One or two items read inline, each path shortened to repo/…/file;
// more collapse to "18 files ›" and open one per line. The full text stays the title, so a hover reads it whole.

const INLINE_MAX = 2;

export function CitedList({ text }: { text: string }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const items = splitList(text);
  if (items.length <= INLINE_MAX) {
    return <span className="rl-cited" title={text} data-testid="cited-inline">{items.map(shortPath).join("; ")}</span>;
  }
  const count = t(items.every(isPathLike) ? "common.files" : "runs.log.items", { count: items.length });
  return (
    <span className="rl-cited" data-testid="cited-list">
      <button type="button" className="rl-filter" aria-expanded={open} onClick={() => setOpen((o) => !o)} data-testid="cited-toggle">
        {count} {open ? "‹" : "›"}
      </button>
      {open && (
        <span className="rl-cited-items">
          {items.map((item, i) => <span key={`${i}-${item}`} className="rl-cited-item" title={item} data-testid="cited-item">{shortPath(item)}</span>)}
        </span>
      )}
    </span>
  );
}
