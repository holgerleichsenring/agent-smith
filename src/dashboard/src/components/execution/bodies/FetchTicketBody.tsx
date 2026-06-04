"use client";

import type { RunEvent } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";

// p0184: Fetch-ticket step body — renders the latest TicketFetchedEvent for
// the run. Description is truncated to 320 chars (operator drills into the
// tracker for the full text); attachment count + state + labels surface as
// chips for at-a-glance scanning. Empty fallback when the event hasn't
// landed yet (mid-fetch) or for legacy runs predating p0184.

interface FetchTicketBodyProps {
  events: RunEvent[];
  testId?: string;
}

const DESCRIPTION_MAX = 320;

export function FetchTicketBody({ events, testId = "fetch-ticket-body" }: FetchTicketBodyProps) {
  const latest = pickLatestTicketEvent(events);
  if (!latest) {
    return (
      <div data-testid={testId} className="py-2 text-sm text-stone-400">
        Waiting for ticket fetch…
      </div>
    );
  }
  const truncated = truncate(latest.description, DESCRIPTION_MAX);
  return (
    <div data-testid={testId} className="space-y-2.5 text-sm text-stone-700">
      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <code
          data-testid={`${testId}-id`}
          className="rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-label text-stone-700"
        >
          #{latest.ticketId}
        </code>
        <span data-testid={`${testId}-title`} className="font-medium text-stone-900">
          {latest.title}
        </span>
        <span
          data-testid={`${testId}-state`}
          className="rounded-full bg-stone-100 px-2 py-0.5 dsh-label uppercase tracking-wide text-stone-600"
        >
          {latest.state}
        </span>
        <span
          data-testid={`${testId}-attachments`}
          className="rounded-full bg-stone-100 px-2 py-0.5 dsh-label text-stone-600"
        >
          {latest.attachmentCount} attachment{latest.attachmentCount === 1 ? "" : "s"}
        </span>
      </div>
      {latest.labels.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {latest.labels.map((label) => (
            <span
              key={label}
              className="rounded-full border border-stone-200 px-2 py-0.5 dsh-label text-stone-600"
            >
              {label}
            </span>
          ))}
        </div>
      )}
      {truncated.length > 0 && (
        <p
          data-testid={`${testId}-description`}
          className="whitespace-pre-wrap text-stone-600"
        >
          {truncated}
        </p>
      )}
    </div>
  );
}

function pickLatestTicketEvent(events: RunEvent[]) {
  const candidates = events.filter(
    (e): e is Extract<RunEvent, { type: EventType.TicketFetched }> =>
      e.type === EventType.TicketFetched,
  );
  if (candidates.length === 0) return null;
  return candidates.reduce((latest, e) =>
    e.timestamp.localeCompare(latest.timestamp) > 0 ? e : latest,
  );
}

function truncate(text: string, max: number): string {
  if (text.length <= max) return text;
  return text.slice(0, max - 1).trimEnd() + "…";
}
