"use client";

import { useT } from "@/hooks/useT";
import type { PocUploadMatch } from "@/lib/pocsApi";
import { portSentence } from "./ComparedPanel";

// 2026-11-23-933db: the upload differs so much from the PoC it would replace that it is probably the wrong ZIP. Three
// ways on: replace anyway (the import then says it was confirmed), import it as a new app, or choose another ZIP.

export function MismatchAlert({ match, port, onReplace, onNew, onAnother }: {
  match: PocUploadMatch; port: number; onReplace: () => void; onNew: () => void; onAnother: () => void;
}) {
  const t = useT();
  const differ = match.added + match.changed + match.removed;
  return (
    <div className="poc-alert" role="alert" data-testid="poc-import-mismatch">
      <h2>{t("pocs.import.mismatch.title", { name: match.pocName })}</h2>
      <p>
        {t("pocs.import.mismatch.body", { differ, total: differ + match.unchanged })}
        {match.portChanged && <> {portSentence(t, match, port)}</>}
        {match.entrypointChanged && <> {t("pocs.import.compared.entrypoint")}</>}
        {" "}{t("pocs.import.mismatch.usually")}
      </p>
      <div className="poc-bar">
        <button type="button" className="btn" onClick={onReplace} data-testid="poc-import-mismatch-replace">
          {t("pocs.import.mismatch.replace", { name: match.pocName })}
        </button>
        <button type="button" className="btn" onClick={onNew} data-testid="poc-import-mismatch-new">
          {t("pocs.import.mismatch.asNew")}
        </button>
        <button type="button" className="btn" onClick={onAnother} data-testid="poc-import-mismatch-another">
          {t("pocs.import.mismatch.another")}
        </button>
      </div>
      <div className="poc-meta">{t("pocs.import.mismatch.limits")}</div>
    </div>
  );
}
