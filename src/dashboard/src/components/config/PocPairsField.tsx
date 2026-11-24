"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import { NO_AUTOFILL } from "./formFields";

// 2026-11-23-c81eb: a name/value map edited as rows (ingress annotations, pod labels). The rows live here, so a row
// whose name is still empty can be typed into; the map handed back leaves such rows out, and a name typed twice
// keeps its last value. Names such as "nginx.ingress.kubernetes.io/auth-url" are sent as they are.

type Row = { name: string; value: string };

const rowsOf = (map: Record<string, string>): Row[] => Object.entries(map).map(([name, value]) => ({ name, value }));

function mapOf(rows: Row[]): Record<string, string> {
  const map: Record<string, string> = {};
  for (const row of rows) if (row.name.trim()) map[row.name.trim()] = row.value;
  return map;
}

const same = (a: Record<string, string>, b: Record<string, string>) => JSON.stringify(a) === JSON.stringify(b);

export function PocPairsField({ label, help, value, testId, namePlaceholder, valuePlaceholder, onChange }: {
  label: string; help?: string; value: Record<string, string>; testId: string;
  namePlaceholder?: string; valuePlaceholder?: string; onChange: (map: Record<string, string>) => void;
}) {
  const t = useT();
  const [rows, setRows] = useState<Row[]>(() => rowsOf(value));
  const [synced, setSynced] = useState(value);
  // A value changed from outside (another target loaded, a reset) replaces the rows.
  if (!same(value, synced) && !same(value, mapOf(rows))) {
    setRows(rowsOf(value));
    setSynced(value);
  }
  const change = (next: Row[]) => {
    const map = mapOf(next);
    setRows(next);
    setSynced(map);
    onChange(map);
  };
  const setAt = (n: number, patch: Partial<Row>) => change(rows.map((row, m) => (m === n ? { ...row, ...patch } : row)));
  return (
    <div className="field" data-testid={testId}>
      <label>
        {label}
        {help && <span className="help">{help}</span>}
      </label>
      {rows.length === 0 && <span className="help" data-testid={`${testId}-none`}>{t("config.pocs.pairs.none")}</span>}
      {rows.map((row, n) => (
        <div key={n} style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <input type="text" className="mono" aria-label={t("config.pocs.pairs.name")} data-testid={`${testId}-${n}-name`}
            name={`${testId}-${n}-name`} {...NO_AUTOFILL}
            value={row.name} placeholder={namePlaceholder} onChange={(e) => setAt(n, { name: e.target.value })} />
          <input type="text" className="mono" aria-label={t("config.pocs.pairs.value")} data-testid={`${testId}-${n}-value`}
            name={`${testId}-${n}-value`} {...NO_AUTOFILL}
            value={row.value} placeholder={valuePlaceholder} onChange={(e) => setAt(n, { value: e.target.value })} />
          <button type="button" className="btn" data-testid={`${testId}-remove-${n}`}
            onClick={() => change(rows.filter((_, m) => m !== n))}>
            {t("config.pocs.pairs.remove")}
          </button>
        </div>
      ))}
      <div>
        <button type="button" className="btn" data-testid={`${testId}-add`} onClick={() => setRows([...rows, { name: "", value: "" }])}>
          {t("config.pocs.pairs.add")}
        </button>
      </div>
    </div>
  );
}
