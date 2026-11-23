"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { pocRevisionPagePath } from "@/lib/pocRevisionPath";
import { getPocRevision, type PocRevisionView } from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { AgentExplanation } from "./AgentExplanation";
import { BlockingFinding } from "./BlockingFinding";
import { OverruleBox } from "./OverruleBox";
import { ScanNotes } from "./ScanNotes";
import { StepStrip } from "./StepStrip";
import "@/styles/pocs.css";

// 2026-11-23-933dc: /pocs/{pocId}/revisions/{n} — why a revision is blocked, or what its scan noted and where it was
// committed. The overrule box is there only when the server says the caller may overrule; otherwise its sentence why not.

const POCS_LIST_PATH = "/pocs";
const POCS_IMPORT_PATH = "/pocs/import";

type Load = { status: "loading" } | { status: "missing" } | { status: "failed"; message: string } | { status: "ok"; view: PocRevisionView };

export function PocRevisionPage({ pocId, revision }: { pocId: number; revision: number }) {
  const t = useT();
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const valid = Number.isInteger(pocId) && Number.isInteger(revision) && pocId > 0 && revision > 0;

  useEffect(() => {
    if (!valid) return;
    const abort = new AbortController();
    getPocRevision(pocId, revision, abort.signal)
      .then((view) => setLoad(view ? { status: "ok", view } : { status: "missing" }))
      .catch((err: unknown) => {
        if (abort.signal.aborted) return;
        setLoad({ status: "failed", message: err instanceof Error ? err.message : String(err) });
      });
    return () => abort.abort();
  }, [pocId, revision, valid]);

  if (load.status === "ok") return <PocRevisionDetail view={load.view} />;
  const sentence = !valid || load.status === "missing" ? t("pocs.revision.notFound")
    : load.status === "failed" ? t("pocs.revision.failed", { message: load.message })
    : t("pocs.revision.loading");
  const testId = !valid ? "missing" : load.status;
  return (
    <>
      <div className="poc-crumb"><Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link></div>
      <div className="msub note" data-testid={`poc-revision-${testId}`}>{sentence}</div>
    </>
  );
}

export function PocRevisionDetail({ view }: { view: PocRevisionView }) {
  const t = useT();
  const blocked = view.state === "blocked";
  const blocks = view.verdict?.blocks ?? [];
  const notes = view.verdict?.notes ?? [];
  return (
    <div data-testid="poc-revision" data-state={view.state}>
      <div className="poc-crumb">
        <Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link> / {view.name} / {t("pocs.revision.crumb", { revision: view.revision })}
      </div>
      <div className="m-head">
        <div>
          <h1 className="poc-rev-title">
            {t("pocs.revision.title", { revision: view.revision })}
            <span className={`poc-badge ${blocked || view.state === "refused" ? "bad" : view.state === "admitted" ? "run" : "ok"}`}
              data-testid="poc-revision-state">
              <span className="poc-badge-dot" aria-hidden="true" />{stateLabel(t, view.state)}
            </span>
          </h1>
          <div className="msub" data-testid="poc-revision-sub">
            {blocked
              ? t("pocs.revision.sub.blocked", { count: blocks.length })
              : view.commitSha
                ? t("pocs.revision.sub.committed", { sha: view.commitSha.slice(0, 12) })
                : t("pocs.revision.sub.notCommitted")}
            {view.runId && <> <Link href={runPath(view.runId)} data-testid="poc-revision-run">{t("pocs.revision.run")} →</Link></>}
          </div>
          {view.latestRevision > view.revision && (
            <div className="poc-meta" data-testid="poc-revision-newer">
              {t("pocs.revision.newer")}{" "}
              <Link href={pocRevisionPagePath(view.pocId, view.latestRevision)}>r{view.latestRevision}</Link>
            </div>
          )}
        </div>
        {blocked && (
          <Link className="btn primary" href={POCS_IMPORT_PATH} data-testid="poc-revision-import">{t("pocs.revision.importFixed")}</Link>
        )}
      </div>
      <StepStrip view={view} />
      <div className="poc-cols">
        <div className="poc-col-main">
          {blocks.map((finding) => <BlockingFinding key={finding.id} finding={finding} />)}
          {view.verdict === null && <div className="poc-meta" data-testid="poc-revision-not-scanned">{t("pocs.revision.notScanned")}</div>}
          {view.verdict !== null && blocks.length === 0 && (
            <div className="poc-meta" data-testid="poc-revision-no-blocks">{t("pocs.revision.noBlocks")}</div>
          )}
          <ScanNotes notes={notes} open={!blocked} />
        </div>
        <div className="poc-col-side">
          {(blocked || view.explanation) && <AgentExplanation explanation={view.explanation} />}
          {blocked && (view.mayOverrule
            ? <OverruleBox pocId={view.pocId} revision={view.revision} />
            : view.whyNot && <div className="poc-why-not" data-testid="poc-revision-why-not">{view.whyNot}</div>)}
        </div>
      </div>
    </div>
  );
}

const STATE_LABELS = {
  admitted: "pocs.revision.state.admitted",
  validated: "pocs.revision.state.validated",
  refused: "pocs.revision.state.refused",
  blocked: "pocs.revision.state.blocked",
  overruled: "pocs.revision.state.overruled",
} as const;

function stateLabel(t: T, state: string): string {
  return state in STATE_LABELS ? t(STATE_LABELS[state as keyof typeof STATE_LABELS]) : state;
}
