"use client";

import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import type { PocUploadMatch } from "@/lib/pocsApi";

// 2026-11-23-933db: how the upload compares with the PoC it looks like — files added, changed, removed and unchanged,
// the share that differs, and the port and start command when they change.

export function ComparedPanel({ match, port }: { match: PocUploadMatch; port: number }) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-import-compared">
      <h2>
        {match.revision === null
          ? t("pocs.import.compared.titleNoRevision", { name: match.pocName })
          : t("pocs.import.compared.title", { revision: match.revision })}
      </h2>
      <div className="poc-diffnums">
        <span className="add">{t("pocs.import.compared.added", { count: match.added })}</span>
        <span className="chg">{t("pocs.import.compared.changed", { count: match.changed })}</span>
        <span className="del">{t("pocs.import.compared.removed", { count: match.removed })}</span>
        <span className="same">{t("pocs.import.compared.unchanged", { count: match.unchanged })}</span>
      </div>
      <ul className="poc-compared">
        <li>{t("pocs.import.compared.ratio", { percent: match.ratioPercent })}</li>
        <li>{portSentence(t, match, port)}</li>
        {match.entrypointChanged && <li>{t("pocs.import.compared.entrypoint")}</li>}
      </ul>
    </section>
  );
}

/** The port as it was, or how it changes. */
export function portSentence(t: T, match: PocUploadMatch, port: number): string {
  if (!match.portChanged) return t("pocs.import.compared.port", { port });
  return match.previousPort === null
    ? t("pocs.import.compared.portChangedTo", { to: port })
    : t("pocs.import.compared.portChanged", { from: match.previousPort, to: port });
}
