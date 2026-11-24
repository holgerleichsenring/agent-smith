"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { pocRolloutPagePath } from "@/lib/pocRevisionPath";
import {
  getPocDetail, getPocRevisions, postPocAct, type PocActed, type PocActBody, type PocAct, type PocDetail, type PocRevisionRow,
} from "@/lib/pocsApi";
import { ActButtons } from "./ActButtons";
import { actRefusal } from "./actRefusal";
import { AuditList } from "./AuditList";
import { DesiredState } from "./DesiredState";
import { PocDetailVariables } from "./PocDetailVariables";
import { PocHeader } from "./PocHeader";
import { ReadinessSetting } from "./ReadinessSetting";
import { RevisionTable } from "./RevisionTable";
import "@/styles/pocs.css";

// 2026-11-23-c81ed: /pocs/{pocId} — one PoC: its header, what its GitOps folder asks for, its revisions with their
// rollouts, the acts its state admits, the readiness probe, its variables and the audit of its acts. Every act is a
// run; once one is queued the page links its Rollout page and reads the PoC again.

const POCS_LIST_PATH = "/pocs";

type Load =
  | { status: "loading" }
  | { status: "missing" }
  | { status: "failed"; message: string }
  | { status: "ok"; poc: PocDetail; revisions: PocRevisionRow[] };

export function PocDetailPage({ pocId }: { pocId: number }) {
  const t = useT();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const valid = Number.isInteger(pocId) && pocId > 0;

  const read = useCallback(async (signal?: AbortSignal) => {
    try {
      const [poc, list] = await Promise.all([getPocDetail(pocId, signal), getPocRevisions(pocId, signal)]);
      if (signal?.aborted) return;
      setLoad(poc ? { status: "ok", poc, revisions: list?.revisions ?? [] } : { status: "missing" });
    } catch (err: unknown) {
      if (signal?.aborted) return;
      setLoad({ status: "failed", message: err instanceof Error ? err.message : String(err) });
    }
  }, [pocId]);

  useEffect(() => {
    if (!valid) return;
    const abort = new AbortController();
    void read(abort.signal);
    return () => abort.abort();
  }, [read, valid]);

  if (load.status === "ok") {
    return <PocDetailView poc={load.poc} revisions={load.revisions} reread={() => void read()} />;
  }
  const sentence = !valid || load.status === "missing" ? t("pocs.detail.notFound")
    : load.status === "failed" ? t("pocs.detail.failed", { message: load.message })
    : t("pocs.detail.loading");
  return (
    <>
      <div className="poc-crumb"><Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link></div>
      <div className="msub note" data-testid={`poc-detail-${!valid ? "missing" : load.status}`}>{sentence}</div>
    </>
  );
}

const ACT_STARTED: Record<PocAct, TextKey> = {
  stop: "pocs.detail.started.stop",
  start: "pocs.detail.started.start",
  rollback: "pocs.detail.started.rollback",
  apply: "pocs.detail.started.apply",
  delete: "pocs.detail.started.delete",
};

export function PocDetailView({ poc, revisions, reread }: { poc: PocDetail; revisions: PocRevisionRow[]; reread: () => void }) {
  const t = useT();
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [acted, setActed] = useState<{ act: PocAct; run: PocActed } | null>(null);

  const onAct = async (act: PocAct, body: PocActBody = {}): Promise<boolean> => {
    setBusy(true);
    setRefusal(null);
    try {
      const run = await postPocAct(poc.id, act, body);
      setActed({ act, run });
      reread();
      return true;
    } catch (err) {
      setRefusal(actRefusal(t, err));
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="poc-detail" data-testid="poc-detail" data-state={poc.state}>
      <PocHeader poc={poc} />
      {acted && (
        <div className="poc-acted" role="status" data-testid="poc-detail-acted" data-act={acted.act}>
          {t(ACT_STARTED[acted.act])}{" "}
          <Link href={pocRolloutPagePath(poc.id, acted.run.runId)} data-testid="poc-detail-acted-run">{t("pocs.rollout.title")} →</Link>
        </div>
      )}
      {refusal && <div className="poc-refusal" role="alert" data-testid="poc-detail-refusal">{refusal}</div>}
      <div className="poc-cols poc-detail-cols">
        <div className="poc-col-main">
          <RevisionTable pocId={poc.id} revisions={revisions} mayRollBack={poc.acts.rollback} busy={busy} onAct={onAct} />
          <PocDetailVariables pocId={poc.id} mayAct={poc.mayAct} onSaved={reread} />
          <AuditList pocId={poc.id} audit={poc.audit} />
        </div>
        <div className="poc-col-side">
          <DesiredState desired={poc.desired} />
          <ActButtons poc={poc} busy={busy} onAct={onAct} />
          <ReadinessSetting key={poc.desired.readinessPath ?? "tcp"} current={poc.desired.readinessPath}
            maxChars={poc.readinessPathMaxChars} enabled={poc.acts.apply} busy={busy} onAct={onAct} />
        </div>
      </div>
    </div>
  );
}
