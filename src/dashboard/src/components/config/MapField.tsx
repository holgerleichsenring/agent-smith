"use client";

import { useRef, useState } from "react";

// p0499: a string->string map, edited as one ROW PER ENTRY — a key input and a value
// input, never a line of text. The previous control was a textarea of `key: value`
// lines split at the FIRST colon, and the operator's label keys contain colons: five
// entries parsed to one, so opening the tracker dialog, touching the field and saving
// wiped the whole label-to-pipeline routing. Two inputs cannot be split wrongly, and a
// key containing a colon is just a string.

type Row = { id: number; key: string; value: string };

export function MapField({
  label,
  values,
  onChange,
  testId,
  help,
}: {
  label: string;
  values: Record<string, string>;
  onChange: (v: Record<string, string> | undefined) => void;
  testId?: string;
  help?: string;
}) {
  const [rows, setRows] = useState<Row[]>(() => toRows(values));
  const nextId = useRef(rows.length);
  const emitted = useRef<Record<string, string> | null>(null);

  // The rows are the state, not a view of the record: a row being typed has an empty
  // key and CANNOT exist in a Record, so deriving rows from the value every render is
  // what swallows a half-typed line. Reseed only when the incoming value disagrees
  // with what this field last EMITTED — that is the one case where the change came
  // from somewhere else (the drawer switched entity, or a type change pruned the key).
  if (emitted.current !== null && !sameMap(emitted.current, values)) {
    emitted.current = null;
    nextId.current = Object.keys(values).length;
    setRows(toRows(values));
  }

  const apply = (next: Row[]) => {
    setRows(next);
    const record = toRecord(next);
    emitted.current = record;
    onChange(Object.keys(record).length > 0 ? record : undefined);
  };

  const edit = (id: number, patch: Partial<Row>) =>
    apply(rows.map((r) => (r.id === id ? { ...r, ...patch } : r)));

  return (
    <div className="field" data-testid={testId}>
      <label>
        {label} <span className="help">{help ?? "one key and one value per row"}</span>
      </label>
      <div className="maprows">
        {rows.map((row, i) => (
          <div className="maprow" key={row.id}>
            <input
              type="text"
              className="mono"
              aria-label={`${label} key ${i + 1}`}
              data-testid={testId && `${testId}-key-${i}`}
              value={row.key}
              onChange={(e) => edit(row.id, { key: e.target.value })}
            />
            <input
              type="text"
              className="mono"
              aria-label={`${label} value ${i + 1}`}
              data-testid={testId && `${testId}-value-${i}`}
              value={row.value}
              onChange={(e) => edit(row.id, { value: e.target.value })}
            />
            <button
              type="button"
              className="pick"
              aria-label={`Remove ${label} row ${i + 1}`}
              data-testid={testId && `${testId}-remove-${i}`}
              onClick={() => apply(rows.filter((r) => r.id !== row.id))}
            >
              ×
            </button>
          </div>
        ))}
        {rows.length === 0 && <span className="help">no entries</span>}
      </div>
      <button
        type="button"
        className="pick"
        data-testid={testId && `${testId}-add`}
        onClick={() => setRows([...rows, { id: nextId.current++, key: "", value: "" }])}
      >
        + add entry
      </button>
    </div>
  );
}

const toRows = (values: Record<string, string>): Row[] =>
  Object.entries(values).map(([key, value], id) => ({ id, key, value }));

/** Rows without a key are still being written and simply have nothing to contribute. */
function toRecord(rows: readonly Row[]): Record<string, string> {
  return Object.fromEntries(rows.filter((r) => r.key.trim().length > 0).map((r) => [r.key, r.value]));
}

function sameMap(a: Record<string, string>, b: Record<string, string>): boolean {
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((k) => a[k] === b[k]);
}
