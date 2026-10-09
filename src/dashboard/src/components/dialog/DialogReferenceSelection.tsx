"use client";

import { useMemo, useState } from "react";
import type { SpecDialogHeldContent } from "@/types/spec-dialog";
import {
  leftOutOf,
  megabytes,
  sentBy,
  totalsOf,
  type PickLeftOut,
  type SelectionEntry,
} from "./referenceSelection";
import { useSelectionEntries } from "./useSelectionEntries";

// 2026-10-02-075db: a picked folder or file set, shown before it is sent. Each top-level entry
// with what it sends; rebuildable folders and what the pick's own .gitignore names start
// unticked and say why; the operator ticks what goes, so a cache folder no rule knows is one
// click. Send is held while the selection is over the set's bounds, saying by how much — the
// server refuses such a body unread. Cancel drops the pick.
// 2026-10-09-86e1: what the conversation already holds is marked before anything shows — a loose
// file unticked with where it is, a folder with how many of its files — and stays the operator's
// to tick: they own the risk of sending it again.

export function DialogReferenceSelection({
  files,
  onSend,
  onCancel,
  left = null,
  loadHeld,
}: {
  files: File[];
  /** 2026-10-09-86e1: the content hashes the conversation holds; absent before one is open. */
  loadHeld?: () => Promise<SpecDialogHeldContent[]>;
  /** 2026-10-08-e8b9h: what the conversation has left under its byte cap; Send is held past it. */
  left?: number | null;
  onSend: (files: File[], leftOut: PickLeftOut) => void;
  onCancel: () => void;
}) {
  const read = useSelectionEntries(files, loadHeld);
  const entries = read?.entries ?? null;
  // The operator's ticks, held against the read they were made on; a new read starts from its own.
  const [chosen, setChosen] = useState<{ on: typeof read; ticks: boolean[] } | null>(null);
  const ticks = chosen && chosen.on === read ? chosen.ticks : (entries ?? []).map((entry) => entry.ticked);
  const setTicks = (next: boolean[]) => setChosen({ on: read, ticks: next });

  const sent = useMemo(
    () => (entries ?? []).flatMap((entry, i) => sentBy(entry, ticks[i] ?? false)),
    [entries, ticks],
  );
  const totals = totalsOf(sent, left);
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
      {read && !read.checked && (
        <p className="d-pick-total" data-testid="dialog-reference-selection-unchecked">
          Could not check which of these files this conversation already holds; the server still refuses a pick it holds entirely.
        </p>
      )}
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
        {entry.held && <span className="d-pick-held" data-testid={`dialog-reference-held-${entry.name}`}>{entry.held}</span>}
      </span>
    </li>
  );
}

function countOf(files: number): string {
  return files === 1 ? "1 file" : `${files} files`;
}
