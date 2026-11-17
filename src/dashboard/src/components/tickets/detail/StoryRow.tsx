"use client";

import type { ReactNode } from "react";
import { clock } from "./flowWords";
import type { StoryLine } from "./storyLines";

// 2026-11-17-8d1cb: one line of What happened (TicketRail mock) — time, a dot in the outcome's colour, who, the outcome
// and one sentence, how long, and ▸ when there is more to open. Hairlines and text colour only; the body opens under it.

interface Props {
  line: StoryLine;
  open: boolean;
  /** Absent: the line has nothing more to show and draws no chevron. */
  onToggle?: () => void;
  testId: string;
  /** "stopped" for a run that stopped, so the line reads in the stopped colour. */
  className?: string;
  children?: ReactNode;
}

export function StoryRow({ line, open, onToggle, testId, className, children }: Props) {
  return (
    <div className={["tk-line", line.tone, className].filter(Boolean).join(" ")} data-testid={testId} data-open={open || undefined}>
      <button type="button" className="tk-line-head" aria-expanded={onToggle ? open : undefined} onClick={onToggle}
        disabled={!onToggle} data-testid={`${testId}-head`}>
        <span className="tk-at mono">{clock(line.at)}</span>
        <span className="tk-dot" aria-hidden="true" />
        <span className="tk-who">{line.who}</span>
        <span className="tk-text">
          {line.outcome && <span className="tk-outcome">{line.outcome}</span>}
          {line.outcome && " · "}
          {line.text}
        </span>
        <span className="tk-meta">{line.meta}</span>
        <span className="tk-chev" aria-hidden="true">{onToggle ? (open ? "▾" : "▸") : ""}</span>
      </button>
      {open && children && <div className="tk-line-body">{children}</div>}
    </div>
  );
}
