"use client";

import { useT } from "@/hooks/useT";
import type { PocImportChoice, PocUploadMatch } from "@/lib/pocsApi";
import { cn } from "@/lib/utils";

// 2026-11-23-933db: which app the ZIP is — the PoC it looks like, or a new one. Asked only when the upload matched one.

export function AppChoice({ match, choice, onChoice }: {
  match: PocUploadMatch; choice: PocImportChoice; onChoice: (choice: PocImportChoice) => void;
}) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-import-app">
      <h2>{t("pocs.import.app.title")}</h2>
      <div className="poc-choices" role="radiogroup">
        <Choice on={choice === "replace"} onPick={() => onChoice("replace")} testId="poc-import-app-replace"
          title={t("pocs.import.app.replace", { name: match.pocName })} hint={t("pocs.import.app.replace.hint", { name: match.pocName })} />
        <Choice on={choice === "new"} onPick={() => onChoice("new")} testId="poc-import-app-new"
          title={t("pocs.import.app.new")} hint={t("pocs.import.app.new.hint")} />
      </div>
    </section>
  );
}

function Choice({ on, onPick, title, hint, testId }: {
  on: boolean; onPick: () => void; title: string; hint: string; testId: string;
}) {
  return (
    <label className={cn("poc-choice", on && "on")}>
      <input type="radio" name="poc-import-app" checked={on} onChange={onPick} data-testid={testId} />
      <span><b>{title}</b><span className="poc-meta">{hint}</span></span>
    </label>
  );
}
