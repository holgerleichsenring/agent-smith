"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import {
  getPocVariables, putPocVariables, type PocVariableInput, type PocVariablesChange, type PocVariablesWritten, type PocVariableView,
} from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { PocVariablesTable, variablesToSend, withoutSecretValues } from "../import/PocVariablesTable";
import { actRefusal } from "./actRefusal";

// 2026-11-23-c81ed: 933dg's variables table on the detail page. A secret's value is never read back: its row shows an
// empty field, and an empty secret field keeps the stored value. Saving writes the change through a poc-variables run,
// and the server queues an apply run at once — or says why it could not, in which case Apply or the next deploy does.

export function rowsOf(views: PocVariableView[]): PocVariableInput[] {
  return views.map((view) => ({ name: view.name, secret: view.kind === "secret", value: view.value ?? "" }));
}

/** What the rows change against what is stored: new, re-kinded or re-valued names are set, a missing name is deleted.
 *  A stored secret whose field was left empty is kept as it is. */
export function variablesChange(stored: PocVariableView[], rows: PocVariableInput[]): PocVariablesChange {
  const sent = variablesToSend(rows);
  const byName = new Map(stored.map((view) => [view.name, view]));
  const set = sent.filter((row) => {
    const was = byName.get(row.name);
    if (!was || (was.kind === "secret") !== row.secret) return true;
    return row.secret ? row.value !== "" : row.value !== (was.value ?? "");
  });
  const names = new Set(sent.map((row) => row.name));
  return { set, delete: stored.filter((view) => !names.has(view.name)).map((view) => view.name) };
}

type Load = { status: "loading" } | { status: "failed"; message: string } | { status: "ok"; stored: PocVariableView[] };

export function PocDetailVariables({ pocId, mayAct, onSaved }: { pocId: number; mayAct: boolean; onSaved: () => void }) {
  const t = useT();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [rows, setRows] = useState<PocVariableInput[]>([]);
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [written, setWritten] = useState<PocVariablesWritten | null>(null);

  const read = useCallback(async (signal?: AbortSignal) => {
    try {
      const { variables } = await getPocVariables(pocId, signal);
      if (signal?.aborted) return;
      setLoad({ status: "ok", stored: variables });
      setRows(rowsOf(variables));
    } catch (err: unknown) {
      if (signal?.aborted) return;
      setLoad({ status: "failed", message: err instanceof Error ? err.message : String(err) });
    }
  }, [pocId]);

  useEffect(() => {
    const abort = new AbortController();
    void read(abort.signal);
    return () => abort.abort();
  }, [read]);

  if (load.status !== "ok") {
    return (
      <section className="poc-box" data-testid="poc-detail-variables">
        <h2>{t("pocs.variables.title")}</h2>
        <p className="poc-meta" data-testid={`poc-detail-variables-${load.status}`}>
          {load.status === "loading" ? t("pocs.detail.variables.loading") : t("pocs.detail.variables.failed", { message: load.message })}
        </p>
      </section>
    );
  }

  const change = variablesChange(load.stored, rows);
  const changed = change.set.length > 0 || change.delete.length > 0;
  const save = async () => {
    setBusy(true);
    setRefusal(null);
    setWritten(null);
    try {
      const answer = await putPocVariables(pocId, change);
      setRows(withoutSecretValues);
      setWritten(answer);
      await read();
      onSaved();
    } catch (err) {
      setRefusal(actRefusal(t, err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="poc-detail-variables" data-testid="poc-detail-variables">
      <PocVariablesTable rows={rows} onChange={setRows} disabled={!mayAct || busy} testId="poc-detail-variables-table" />
      <div className="poc-box poc-variables-save">
        <p className="poc-meta">{t("pocs.detail.variables.note")}</p>
        <div className="poc-bar">
          <button type="button" className="btn primary" disabled={!mayAct || busy || !changed} data-testid="poc-detail-variables-save"
            onClick={() => void save()}>
            {busy ? t("pocs.detail.variables.saving") : t("pocs.detail.variables.save")}
          </button>
        </div>
        {written && (
          <div className="poc-meta" data-testid="poc-detail-variables-written">
            <span>{written.line}</span>{" "}
            <Link href={runPath(written.runId)} data-testid="poc-detail-variables-run">{t("pocs.revision.run")} →</Link>
            {written.applyRunId ? (
              <span data-testid="poc-detail-variables-apply">
                {" "}{t("pocs.detail.variables.applying")}{" "}
                <Link href={runPath(written.applyRunId)} data-testid="poc-detail-variables-apply-run">{t("pocs.detail.variables.applyRun")} →</Link>
              </span>
            ) : written.applyNote ? (
              <span className="poc-apply-note" data-testid="poc-detail-variables-apply-note">{written.applyNote}</span>
            ) : null}
          </div>
        )}
        {refusal && <div className="poc-refusal" role="alert" data-testid="poc-detail-variables-refusal">{refusal}</div>}
      </div>
    </div>
  );
}
