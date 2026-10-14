"use client";

import type { TicketSeriesBuildNote } from "@/types/tickets";

// 2026-10-13-8762m: what an executed spec's master handed the next spec — renamed, moved, persisted and
// deviations, read out of the spec's record — or "no note" when it left none.

const LISTS: Array<[keyof Omit<TicketSeriesBuildNote, "missing">, string]> = [
  ["renamed", "Renamed"],
  ["moved", "Moved"],
  ["persisted", "Persisted"],
  ["deviations", "Deviations"],
];

export function BuildNote({ note }: { note: TicketSeriesBuildNote }) {
  const lists = LISTS.filter(([key]) => note[key].length > 0);
  return (
    <div className="blk" data-testid="spec-build-note">
      <h4>Build note</h4>
      {note.missing || lists.length === 0 ? (
        <div className="dim-line">{note.missing ? "no note" : "nothing renamed, moved, persisted or deviated from"}</div>
      ) : (
        lists.map(([key, label]) => (
          <div key={key} className="crit">
            <span className="mono">{label}</span>
            {note[key].map((entry) => (
              <div key={entry} className="ci"><span>{entry}</span></div>
            ))}
          </div>
        ))
      )}
    </div>
  );
}
