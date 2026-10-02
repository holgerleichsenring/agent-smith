"use client";

import { useEffect, useMemo, useState } from "react";
import {
  entriesOf,
  gitignoresOf,
  leftOutOf,
  megabytes,
  sentBy,
  totalsOf,
  type PickLeftOut,
  type SelectionEntry,
} from "./referenceSelection";

// 2026-10-02-075db: a picked folder or file set, shown before it is sent. Each top-level entry
// with what it sends; rebuildable folders and what the pick's own .gitignore names start
// unticked and say why; the operator ticks what goes, so a cache folder no rule knows is one
// click. Send is held while the selection is over the set's bounds, saying by how much — the
// server refuses such a body unread. Cancel drops the pick.

export function DialogReferenceSelection({
  files,
  onSend,
  onCancel,
}: {
  files: File[];
  onSend: (files: File[], leftOut: PickLeftOut) => void;
  onCancel: () => void;
}) {
  const [entries, setEntries] = useState<SelectionEntry[] | null>(null);
  const [ticks, setTicks] = useState<boolean[]>([]);

  // The .gitignore files are read before anything is shown: an entry shown ticked that then
  // unticks itself is a card that changed under the operator's hand.
  useEffect(() => {
    let live = true;
    void gitignoresOf(files).then((gitignores) => {
      if (!live) return;
      const read = entriesOf(files, gitignores);
      setEntries(read);
      setTicks(read.map((entry) => entry.ticked));
    });
    return () => {
      live = false;
    };
  }, [files]);

  const sent = useMemo(
    () => (entries ?? []).flatMap((entry, i) => sentBy(entry, ticks[i] ?? false)),
    [entries, ticks],
  );
  const totals = totalsOf(sent);
  if (!entries) return null;

  return (
    <div className="d-pick" data-testid="dialog-reference-selection">
      <ul className="d-pick-list">
        {entries.map((entry, i) => (
          <EntryRow
            key={entry.name}
            entry={entry}
            ticked={ticks[i]}
            onTick={(ticked) => setTicks(ticks.map((t, j) => (j === i ? ticked : t)))}
          />
        ))}
      </ul>
      <p className={`d-pick-total${totals.over ? " bad" : ""}`} data-testid="dialog-reference-selection-total">
        {`Sending ${countOf(totals.files)}, ${megabytes(totals.bytes)}`}
        {totals.over ? ` — ${totals.over}.` : "."}
      </p>
      <div className="d-pick-actions">
        <button type="button" className="btn" data-testid="dialog-reference-selection-cancel" onClick={onCancel}>
          Cancel
        </button>
        <button
          type="button"
          className="btn primary"
          data-testid="dialog-reference-selection-send"
          disabled={totals.files === 0 || totals.over !== null}
          onClick={() => onSend(sent, leftOutOf(entries, ticks))}
        >
          Send
        </button>
      </div>
    </div>
  );
}

function EntryRow({ entry, ticked, onTick }: { entry: SelectionEntry; ticked: boolean; onTick: (ticked: boolean) => void }) {
  const sending = sentBy(entry, true);
  const bytes = sending.reduce((sum, file) => sum + file.size, 0);
  return (
    <li className="d-pick-row" data-testid={`dialog-reference-entry-${entry.name}`}>
      <label>
        {/* An entry with nothing that could be sent — every file rebuildable or oversized — has
            no tick to give; it is listed so the operator sees what the pick held. */}
        <input
          type="checkbox"
          checked={ticked}
          disabled={sending.length === 0}
          onChange={(event) => onTick(event.target.checked)}
        />
        <span className="mono">{entry.name}</span>
      </label>
      <span className="d-pick-size">
        {ticked || !entry.reason ? `${countOf(sending.length)}, ${megabytes(bytes)}` : entry.reason}
      </span>
    </li>
  );
}

function countOf(files: number): string {
  return files === 1 ? "1 file" : `${files} files`;
}
