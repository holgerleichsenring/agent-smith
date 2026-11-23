"use client";

import { useCallback, useEffect, useState } from "react";
import { deleteSecretValue, fetchSecretValues, type StoredSecretState } from "@/lib/secretValuesApi";
import { useDateLocale, useT } from "@/hooks/useT";

// 2026-11-18-00eaa: values stored in the admin area whose catalog secret was deleted. Deleting the secret leaves its
// value behind; here it can be removed. Delete runs the same `secret` run as the drawer's own Delete stored value.
// A failed read shows nothing here: the secret field beside it already says why the values cannot be read.

export function OrphanedSecretValues() {
  const t = useT();
  const locale = useDateLocale();
  const [orphans, setOrphans] = useState<StoredSecretState[]>([]);
  const [line, setLine] = useState<string | null>(null);
  const reload = useCallback(() => {
    fetchSecretValues().then((view) => setOrphans(view.orphans)).catch(() => setOrphans([]));
  }, []);
  useEffect(reload, [reload]);

  const remove = (name: string) => {
    deleteSecretValue(name).then((r) => setLine(r.line)).catch((e: Error) => setLine(e.message)).finally(reload);
  };

  if (orphans.length === 0 && !line) return null;
  return (
    <div data-testid="secret-orphans" className="flex flex-col gap-2">
      <span className="dsh-label">{t("config.secretOrphans.title")}</span>
      {orphans.map((o) => (
        <div key={o.name} data-testid={`secret-orphan-${o.name}`} className="flex flex-wrap items-center gap-2">
          <span className="mono">{o.name}</span>
          <span className="ec-sub">{t("config.secretOrphans.setBy", { by: o.setBy, at: new Date(o.setAt).toLocaleString(locale) })}</span>
          <button type="button" className="btn" data-testid={`secret-orphan-delete-${o.name}`} onClick={() => remove(o.name)}>
            {t("common.delete")}
          </button>
        </div>
      ))}
      {line && <p data-testid="secret-orphans-line" className="ec-sub">{line}</p>}
    </div>
  );
}
