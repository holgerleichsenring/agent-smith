"use client";

import type { PullRequestOutcomeEvent } from "@/types/hub-events";
import { PrButton } from "@/components/jobs/PrButton";
import { extractUrls, stripUrls } from "@/lib/prUrls";

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
  // p0372: every PR reference renders as the ONE PrButton (same component as
  // the list rows and the Outcome beat).
  if (event.status === "opened" && event.url) {
    return <PrButton url={event.url} testId={`pr-outcome-${event.repo}-link`} />;
  }
  // A failed outcome can still carry a PR (event.url, or a URL embedded in the
  // reason text — typical for a draft PR that survived the failure). It stays
  // clickable: the URL leaves the prose and becomes a draft-toned button.
  const urls = event.url ? [event.url] : extractUrls(event.reason);
  const reason = event.reason ? stripUrls(event.reason) : "";
  return (
    <span className="inline-flex items-center gap-2 text-rose-700">
      <span>failed{reason ? ` — ${reason}` : ""}</span>
      {urls.map((url) => (
        <PrButton key={url} url={url} isDraft testId={`pr-outcome-${event.repo}-link`} />
      ))}
    </span>
  );
}
