"use client";

import Link from "next/link";
import { cn } from "@/lib/utils";

// 2026-11-16-0440b: a list's states as filter pills above it. Each pill is a real link to the
// filtered address, so it can be copied or opened in a tab; a plain click selects in place
// through the list's own filter, which keeps the list mounted.

/** The state colour a pill's dot and count carry. None is a neutral grey dot. */
export type StateTone = "needs" | "person" | "proven" | "stopped";

export interface StatePill<K extends string> {
  key: K;
  label: string;
  href: string;
  /** Omitted while the count is unknown, never a made-up 0. */
  count?: number;
  tone?: StateTone;
}

interface StatePillsProps<K extends string> {
  label: string;
  pills: ReadonlyArray<StatePill<K>>;
  selected: K;
  onSelect: (key: K) => void;
  testId: string;
}

export function StatePills<K extends string>({ label, pills, selected, onSelect, testId }: StatePillsProps<K>) {
  const choose = (key: K) => (event: React.MouseEvent) => {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    onSelect(key);
  };
  return (
    <nav className="st-pills" aria-label={label} data-testid={testId}>
      {pills.map((pill) => (
        <Link
          key={pill.key}
          href={pill.href}
          onClick={choose(pill.key)}
          aria-current={pill.key === selected ? "page" : undefined}
          className={cn("st-pill", pill.key === selected && "on")}
          data-testid={`${testId}-${pill.key}`}
        >
          <span className={cn("st-dot", pill.tone)} aria-hidden="true" />
          {pill.label}
          {pill.count !== undefined && (
            <span className={cn("st-n", pill.count > 0 && pill.tone)} data-testid={`${testId}-${pill.key}-count`}>
              {pill.count}
            </span>
          )}
        </Link>
      ))}
    </nav>
  );
}
