"use client";

import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { TicketSeriesBuildNote } from "@/types/tickets";

// 2026-10-13-8762m: what an executed spec's master handed the next spec — renamed, moved, persisted and
// deviations, read out of the spec's record — or "no note" when it left none.

const LISTS: Array<[keyof Omit<TicketSeriesBuildNote, "missing">, TextKey]> = [
  ["renamed", "tickets.build.renamed"],
  ["moved", "tickets.build.moved"],
  ["persisted", "tickets.build.persisted"],
  ["deviations", "tickets.build.deviations"],
];

export function BuildNote({ note }: { note: TicketSeriesBuildNote }) {
  const t = useT();
  const lists = LISTS.filter(([key]) => note[key].length > 0);
  return (
    <div className="blk" data-testid="spec-build-note">
      <h4>{t("tickets.build.title")}</h4>
      {note.missing || lists.length === 0 ? (
        <div className="dim-line">{note.missing ? t("tickets.build.none") : t("tickets.build.nothing")}</div>
      ) : (
        lists.map(([key, label]) => (
          <div key={key} className="crit">
            <span className="mono">{t(label)}</span>
            {note[key].map((entry) => (
              // 2026-10-25-2a99: .ci is a mark | text | who grid; a lone span sat in the 18px mark column.
              <div key={entry} className="ci" data-testid="build-note-entry">
                <span className="m q">·</span>
                <span>{entry}</span>
              </div>
            ))}
          </div>
        ))
      )}
    </div>
  );
}
