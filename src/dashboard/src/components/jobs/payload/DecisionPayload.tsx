"use client";

import type { DecisionLoggedEvent } from "@/types/hub-events";

export function DecisionPayload({ event }: { event: DecisionLoggedEvent }) {
  return (
    <div className="space-y-2 text-sm" data-testid="decision-payload">
      <header>
        <h3 className="font-medium text-stone-800">{event.category}</h3>
      </header>
      <p className="text-stone-800"><span className="text-stone-500">chose:</span> {event.chose}</p>
      {event.over && (
        <p className="text-stone-800"><span className="text-stone-500">over:</span> {event.over}</p>
      )}
      {event.reason && (
        <p className="whitespace-pre-wrap text-stone-700"><span className="text-stone-500">reason:</span> {event.reason}</p>
      )}
    </div>
  );
}
