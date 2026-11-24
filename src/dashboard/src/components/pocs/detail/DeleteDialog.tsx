"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import type { RunAct } from "./actRefusal";

// 2026-11-23-c81ed: deleting a PoC asks for its name first; the server checks the name it is sent against the PoC's.
// The delete removes the PoC's folder from the GitOps repo; its source folder stays.

export function DeleteDialog({ name, busy, onAct, onClose }: {
  name: string; busy: boolean; onAct: RunAct; onClose: () => void;
}) {
  const t = useT();
  const [typed, setTyped] = useState("");
  const id = "poc-delete-name";
  const submit = async () => {
    if (await onAct("delete", { confirmName: typed })) onClose();
  };
  return (
    <section className="poc-box poc-delete" role="dialog" aria-modal="false" aria-labelledby="poc-delete-title"
      data-testid="poc-detail-delete-dialog">
      <h2 id="poc-delete-title">{t("pocs.detail.delete.title", { name })}</h2>
      <p>{t("pocs.detail.delete.help")}</p>
      <label className="poc-meta" htmlFor={id}>{t("pocs.detail.delete.label", { name })}</label>
      <div className="poc-field">
        <input id={id} type="text" className="mono" value={typed} spellCheck={false} autoComplete="off"
          data-testid="poc-detail-delete-name" onChange={(e) => setTyped(e.target.value)} />
      </div>
      <div className="poc-bar">
        <button type="button" className="btn danger" disabled={busy || typed !== name} data-testid="poc-detail-delete-confirm"
          onClick={() => void submit()}>
          {t("pocs.detail.delete.confirm")}
        </button>
        <button type="button" className="btn" disabled={busy} data-testid="poc-detail-delete-cancel" onClick={onClose}>
          {t("pocs.detail.delete.cancel")}
        </button>
      </div>
    </section>
  );
}
