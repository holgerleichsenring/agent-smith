"use client";

import type { PullRequestOutcomeEvent } from "@/types/hub-events";

// p0223: meaningful per-repo outcome for the commit/PR step. A repo with
// nothing to commit is a normal result ("no changes — no PR needed"), not a
// red failure; a created PR is a clickable link; a genuine failure shows its
// real reason. Renders ABOVE the raw sandbox rows so a red row downstream
// always means something is actually wrong.

interface PrOutcomeListProps {
  events: PullRequestOutcomeEvent[];
}

export function PrOutcomeList({ events }: PrOutcomeListProps) {
  if (events.length === 0) return null;
  return (
    <div data-testid="pr-outcome-list" className="space-y-1">
      {events.map((e) => (
        <PrOutcomeRow key={`${e.repo}-${e.timestamp}`} event={e} />
      ))}
    </div>
  );
}

function PrOutcomeRow({ event }: { event: PullRequestOutcomeEvent }) {
  return (
    <div
      data-testid={`pr-outcome-${event.repo}`}
      data-status={event.status}
      className="flex items-center gap-2 dsh-mono"
    >
      <span className="font-semibold text-stone-700">{event.repo}</span>
      <Outcome event={event} />
    </div>
  );
}

function Outcome({ event }: { event: PullRequestOutcomeEvent }) {
  if (event.status === "no_changes") {
    return <span className="text-stone-500">no changes — no PR needed</span>;
  }
  if (event.status === "opened" && event.url) {
    return (
      <a
        data-testid={`pr-outcome-${event.repo}-link`}
        href={event.url}
        target="_blank"
        rel="noreferrer"
        className="text-emerald-700 underline hover:text-emerald-800"
      >
        PR opened →
      </a>
    );
  }
  return (
    <span className="text-rose-700">
      failed{event.reason ? ` — ${event.reason}` : ""}
    </span>
  );
}
