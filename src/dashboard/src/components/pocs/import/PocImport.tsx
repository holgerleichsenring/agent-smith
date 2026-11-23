"use client";

import Link from "next/link";
import { useState } from "react";
import { usePocsFeature } from "@/hooks/usePocsFeature";
import { useT } from "@/hooks/useT";
import { ApiResponseError } from "@/lib/apiResponse";
import type { T } from "@/lib/i18n/translate";
import {
  importPocUpload, uploadPocZip,
  type PocImportBody, type PocImportChoice, type PocImported, type PocTarget, type PocUpload,
} from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { pocsUnavailable } from "../PocsHome";
import { AppChoice } from "./AppChoice";
import { ComparedPanel } from "./ComparedPanel";
import { DropZone } from "./DropZone";
import { ImportChecks } from "./ImportChecks";
import { MismatchAlert } from "./MismatchAlert";
import { NameField } from "./NameField";
import "@/styles/pocs.css";

// 2026-11-23-933db: /pocs/import — drop one ZIP, read what the server checked, say which app it is, and start the run
// that checks it again. A refusal is shown as the server worded it. Nothing is committed in this release.

export const POCS_PATH = "/pocs";

export function PocImport() {
  const t = useT();
  const { status, list, failed } = usePocsFeature();
  return (
    <>
      <div className="poc-crumb"><Link href={POCS_PATH}>{t("pocs.title")}</Link> / {t("pocs.import.crumb")}</div>
      <div className="m-head">
        <div>
          <h1>{t("pocs.import.title")}</h1>
          <div className="msub">{t("pocs.import.sub")}</div>
        </div>
      </div>
      {status !== "on" || !list
        ? <div className="msub note" data-testid={`poc-import-${failed ? "failed" : status}`}>{pocsUnavailable(t, status, failed)}</div>
        : !list.mayImport
          ? <div className="msub note" data-testid="poc-import-may-not">{t("pocs.import.mayNot")}</div>
          : list.targets.length === 0
            ? <div className="msub note" data-testid="poc-import-no-target">{t("pocs.import.noTarget")}</div>
            : <PocImportFlow targets={list.targets} />}
    </>
  );
}

export function PocImportFlow({ targets }: { targets: PocTarget[] }) {
  const t = useT();
  const [target, setTarget] = useState(targets.length === 1 ? targets[0].name : "");
  const [busy, setBusy] = useState(false);
  const [upload, setUpload] = useState<PocUpload | null>(null);
  const [choice, setChoice] = useState<PocImportChoice>("new");
  const [name, setName] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [started, setStarted] = useState<{ imported: PocImported; name: string } | null>(null);

  const reset = () => {
    setUpload(null);
    setConfirmed(false);
    setRefusal(null);
  };

  const onFile = async (file: File) => {
    reset();
    setBusy(true);
    try {
      const answer = await uploadPocZip(file, targets.length > 1 ? target : undefined);
      setUpload(answer);
      setChoice(answer.match ? "replace" : "new");
      setName(answer.proposedName);
    } catch (err) {
      setRefusal(refusalText(t, err));
    } finally {
      setBusy(false);
    }
  };

  if (started) {
    return (
      <section className="poc-box poc-started" data-testid="poc-import-started">
        <h2>{t("pocs.import.started.title")}</h2>
        <p>{t("pocs.import.started.body", { name: started.name, revision: started.imported.revision })}</p>
        <div className="poc-bar">
          <Link className="btn primary" href={runPath(started.imported.runId)} data-testid="poc-import-run">
            {t("pocs.import.started.run")} →
          </Link>
          <Link className="btn" href={POCS_PATH} data-testid="poc-import-list">{t("pocs.import.started.list")}</Link>
        </div>
      </section>
    );
  }

  const match = upload?.match ?? null;
  const asks = choice === "replace" && match?.verdict === "ask" && !confirmed;
  const canStart = !!upload && !busy && !asks && (choice === "replace" || name.trim() !== "");

  const start = async () => {
    if (!upload) return;
    const body: PocImportBody = choice === "replace" && match
      ? { choice: "replace", pocId: match.pocId, ...(confirmed ? { confirmed: true } : {}) }
      : { choice: "new", target: upload.target, name: name.trim() };
    setBusy(true);
    setRefusal(null);
    try {
      const imported = await importPocUpload(upload.uploadId, body);
      setStarted({ imported, name: body.choice === "replace" && match ? match.pocName : name.trim() });
    } catch (err) {
      setRefusal(refusalText(t, err));
      // A purged or foreign upload cannot be imported any more; the ZIP goes up again.
      if (err instanceof ApiResponseError && (err.status === 404 || err.status === 410)) setUpload(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="poc-cols">
      <div className="poc-col-main">
        <DropZone targets={targets} target={target} onTarget={setTarget} upload={upload} busy={busy && !upload}
          onFile={(file) => void onFile(file)} onAnother={reset} />
        {refusal && <div className="poc-refusal" role="alert" data-testid="poc-import-refusal">{refusal}</div>}
        {upload && match && <AppChoice match={match} choice={choice} onChoice={setChoice} />}
        {upload && choice === "new" && <NameField value={name} onChange={setName} />}
        {upload && match && choice === "replace" && <ComparedPanel match={match} port={upload.port} />}
        {upload && match && asks && (
          <MismatchAlert match={match} port={upload.port}
            onReplace={() => setConfirmed(true)}
            onNew={() => setChoice("new")}
            onAnother={reset} />
        )}
        {upload && match && choice === "replace" && confirmed && (
          <div className="poc-meta" data-testid="poc-import-confirmed">{t("pocs.import.mismatch.confirmed", { name: match.pocName })}</div>
        )}
      </div>
      <div className="poc-col-side">
        {upload && <ImportChecks upload={upload} />}
        <section className="poc-box" data-testid="poc-import-next">
          <h2>{t("pocs.import.next.title")}</h2>
          <ol className="poc-next">
            <li>{t("pocs.import.next.run")}</li>
            <li>{t("pocs.import.next.nothing")}</li>
            <li>{t("pocs.import.next.later")}</li>
          </ol>
        </section>
        <div className="poc-go">
          <button type="button" className="btn primary" disabled={!canStart} onClick={() => void start()}
            data-testid="poc-import-start">
            {busy && upload ? t("pocs.import.starting") : t("pocs.import.start")}
          </button>
          {upload && <span className="poc-meta">{t("pocs.import.targetMeta", { target: upload.target })}</span>}
        </div>
      </div>
    </div>
  );
}

/** The server's sentence as it came; a refusal that said nothing gets one plain line instead. */
function refusalText(t: T, err: unknown): string {
  if (err instanceof ApiResponseError) {
    if (err.reason) return err.reason;
    return err.status === 404 ? t("pocs.import.refused.notFound") : t("pocs.import.refused.unsaid", { status: err.status });
  }
  return t("pocs.import.refused.failed", { message: err instanceof Error ? err.message : String(err) });
}
