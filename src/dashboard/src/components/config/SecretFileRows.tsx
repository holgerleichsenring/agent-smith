"use client";

import { useRef, useState } from "react";
import type { SandboxSecretFile } from "@/lib/configApi";

// 2026-09-22-6c46: a list of three-field rows — the mount path, the Kubernetes Secret and
// the key taken from it. No field primitive in this form vocabulary holds a list of records
// (the map editor is flat string-to-string), which is why this control exists at all; the
// template row is the nearest thing in the tree and is bespoke for the same reason.
//
// THERE IS NO VALUE FIELD, and there never will be: the value lives in the cluster and
// Kubernetes resolves it at mount time. This edits names.

type Row = SandboxSecretFile & { id: number };

const FIELDS = [
  { key: "mount", label: "mount path", placeholder: "/secrets/server.key" },
  { key: "secret", label: "secret name", placeholder: "sf-creds" },
  { key: "key", label: "key in the secret", placeholder: "jwt-key" },
] as const;

export function SecretFileRows({
  label,
  values,
  onChange,
  testId,
  help,
}: {
  label: string;
  values: readonly SandboxSecretFile[];
  onChange: (v: SandboxSecretFile[] | undefined) => void;
  testId: string;
  help?: string;
}) {
  // The rows are the state, not a view of the value: a row being typed is incomplete and
  // could not survive a round trip through the emitted list. Same reasoning as MapField.
  const [rows, setRows] = useState<Row[]>(() => values.map((f, id) => ({ ...f, id })));
  const nextId = useRef(rows.length);

  const apply = (next: Row[]) => {
    setRows(next);
    const emitted = next
      .filter((r) => r.mount.trim() !== "" || r.secret.trim() !== "" || r.key.trim() !== "")
      .map(({ mount, secret, key }) => ({ mount, secret, key }));
    // Deleting the last row means INHERIT — which for this block means "this project
    // declares none", the same way an emptied map emits nothing rather than an empty one.
    onChange(emitted.length > 0 ? emitted : undefined);
  };

  const edit = (id: number, patch: Partial<SandboxSecretFile>) =>
    apply(rows.map((r) => (r.id === id ? { ...r, ...patch } : r)));

  return (
    <div className="field" data-testid={testId}>
      <label>
        {label} <span className="help">{help ?? "mount path, secret name and key — no value"}</span>
      </label>
      <div className="maprows">
        {rows.map((row, i) => (
          <div className="maprow" key={row.id}>
            {FIELDS.map((f) => (
              <input
                key={f.key}
                type="text"
                className="mono"
                aria-label={`${label} ${f.label} ${i + 1}`}
                data-testid={`${testId}-${f.key}-${i}`}
                placeholder={f.placeholder}
                value={row[f.key]}
                onChange={(e) => edit(row.id, { [f.key]: e.target.value })}
              />
            ))}
            <button
              type="button"
              className="pick"
              aria-label={`Remove ${label} row ${i + 1}`}
              data-testid={`${testId}-remove-${i}`}
              onClick={() => apply(rows.filter((r) => r.id !== row.id))}
            >
              ×
            </button>
          </div>
        ))}
        {rows.length === 0 && <span className="help">no mounted secret files</span>}
      </div>
      <button
        type="button"
        className="pick"
        data-testid={`${testId}-add`}
        onClick={() =>
          setRows([...rows, { id: nextId.current++, mount: "", secret: "", key: "" }])
        }
      >
        + add file mount
      </button>
    </div>
  );
}
