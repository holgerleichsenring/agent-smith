"use client";

import { useT } from "@/hooks/useT";

// 2026-11-23-933db: a new PoC's name, prefilled with the server's proposal from the file name. The server checks it.

export function NameField({ value, onChange }: { value: string; onChange: (name: string) => void }) {
  const t = useT();
  return (
    <div className="poc-field">
      <label htmlFor="poc-import-name">{t("pocs.import.name.label")}</label>
      <input id="poc-import-name" type="text" className="mono" value={value} spellCheck={false} autoComplete="off"
        onChange={(e) => onChange(e.target.value)} data-testid="poc-import-name" />
      <span className="poc-meta">{t("pocs.import.name.help")}</span>
    </div>
  );
}
