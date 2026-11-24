"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { pocRevisionPagePath, pocRolloutPagePath } from "@/lib/pocRevisionPath";
import type { PocRevisionRow } from "@/lib/pocsApi";
import type { RunAct } from "./actRefusal";
import { shortDigest } from "./DesiredState";

// 2026-11-23-c81ed: the PoC's revisions newest first — each with its scan state, how its latest rollout ended, its
// digest, its Rollout page, and Roll back on an older one that once ran healthy when the PoC's state admits it.

const REVISION_STATES: Record<string, TextKey> = {
  admitted: "pocs.revision.state.admitted",
  validated: "pocs.revision.state.validated",
  refused: "pocs.revision.state.refused",
  blocked: "pocs.revision.state.blocked",
  overruled: "pocs.revision.state.overruled",
  built: "pocs.revision.state.built",
};

const ROLLOUT_STATES: Record<string, { label: TextKey; dot: string }> = {
  healthy: { label: "pocs.rollout.state.healthy", dot: "ok" },
  failed: { label: "pocs.rollout.state.failed", dot: "bad" },
};

export function RevisionTable({ pocId, revisions, mayRollBack, busy, onAct }: {
  pocId: number; revisions: PocRevisionRow[]; mayRollBack: boolean; busy: boolean; onAct: RunAct;
}) {
  const t = useT();
  return (
    <section className="poc-box poc-revisions" data-testid="poc-detail-revisions">
      <h2>{t("pocs.detail.revisions.title")}</h2>
      {revisions.length === 0 ? <p className="poc-meta">{t("pocs.detail.revisions.none")}</p> : (
        <div className="poc-table-scroll">
          <table className="poc-table">
            <thead>
              <tr>
                <th>{t("pocs.detail.revisions.number")}</th>
                <th>{t("pocs.detail.revisions.state")}</th>
                <th>{t("pocs.detail.revisions.rollout")}</th>
                <th>{t("pocs.detail.desired.digest")}</th>
                <th aria-label={t("pocs.detail.revisions.actions")} />
              </tr>
            </thead>
            <tbody>
              {revisions.map((row) => {
                const id = `poc-detail-revision-${row.number}`;
                const stateKey = REVISION_STATES[row.state];
                const rollout = row.rolloutState ? ROLLOUT_STATES[row.rolloutState] : undefined;
                return (
                  <tr key={row.number} data-testid={id} data-current={row.current ? "true" : undefined}>
                    <td className="mono">
                      <Link href={pocRevisionPagePath(pocId, row.number)} data-testid={`${id}-page`}>r{row.number}</Link>
                      {row.current && <span className="poc-current" data-testid={`${id}-current`}>{t("pocs.detail.revisions.current")}</span>}
                    </td>
                    <td>{stateKey ? t(stateKey) : row.state}</td>
                    <td data-testid={`${id}-rollout`}>
                      {rollout
                        ? <span className="poc-state"><span className={`poc-dot ${rollout.dot}`} aria-hidden="true" />{t(rollout.label)}</span>
                        : <span className="poc-meta">{t("pocs.detail.none")}</span>}
                    </td>
                    <td className="mono" title={row.digest ?? undefined}>{row.digest ? shortDigest(row.digest) : "—"}</td>
                    <td className="poc-row-acts">
                      {row.runId && (
                        <Link href={pocRolloutPagePath(pocId, row.runId)} data-testid={`${id}-rollout-link`}>
                          {t("pocs.rollout.title")} →
                        </Link>
                      )}
                      {row.mayRollBack && mayRollBack && (
                        <button type="button" className="btn" disabled={busy} data-testid={`${id}-rollback`}
                          onClick={() => void onAct("rollback", { revision: row.number })}>
                          {t("pocs.detail.act.rollback")}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
