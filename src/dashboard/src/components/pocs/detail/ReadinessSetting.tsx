"use client";

import { useState } from "react";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { RunAct } from "./actRefusal";

// 2026-11-23-c81ed: how the cluster tells the PoC is ready — an HTTP GET on a path, or a TCP connect to its port. Saving
// it starts an apply run with the new probe. The hints are the client's reading of the server's rule; the server decides.

type Mode = "http" | "tcp";

/** The hints the path draws: it starts with "/" and is at most the server's length. */
export function readinessHints(path: string, maxChars: number): TextKey[] {
  const hints: TextKey[] = [];
  if (!path.startsWith("/")) hints.push("pocs.detail.readiness.hint.slash");
  if (path.length > maxChars) hints.push("pocs.detail.readiness.hint.length");
  return hints;
}

export function ReadinessSetting({ current, maxChars, enabled, busy, onAct }: {
  current: string | null; maxChars: number; enabled: boolean; busy: boolean; onAct: RunAct;
}) {
  const t = useT();
  const [mode, setMode] = useState<Mode>(current ? "http" : "tcp");
  const [path, setPath] = useState(current ?? "/");
  const hints = mode === "http" ? readinessHints(path, maxChars) : [];
  const unchanged = mode === "tcp" ? current === null : path === current;
  const id = "poc-readiness-path";
  return (
    <section className="poc-box poc-readiness" data-testid="poc-detail-readiness">
      <h2>{t("pocs.detail.readiness.title")}</h2>
      <p className="poc-meta">{t("pocs.detail.readiness.help")}</p>
      <div className="poc-choices" role="radiogroup" aria-label={t("pocs.detail.readiness.title")}>
        {(["http", "tcp"] as const).map((value) => (
          <label key={value} className={`poc-choice${mode === value ? " on" : ""}`}>
            <input type="radio" name="poc-readiness" value={value} checked={mode === value} disabled={!enabled || busy}
              data-testid={`poc-detail-readiness-${value}`} onChange={() => setMode(value)} />
            <span>{t(value === "http" ? "pocs.detail.readiness.http" : "pocs.detail.readiness.tcp")}</span>
          </label>
        ))}
      </div>
      {mode === "http" && (
        <div className="poc-field">
          <label htmlFor={id}>{t("pocs.detail.readiness.path")}</label>
          <input id={id} type="text" className="mono" value={path} spellCheck={false} autoComplete="off"
            disabled={!enabled || busy} data-testid="poc-detail-readiness-path" onChange={(e) => setPath(e.target.value)} />
          {hints.map((hint) => (
            <span key={hint} className="poc-meta" data-testid="poc-detail-readiness-hint">{t(hint, { max: maxChars })}</span>
          ))}
        </div>
      )}
      <div className="poc-bar">
        <button type="button" className="btn" disabled={!enabled || busy || hints.length > 0 || unchanged}
          data-testid="poc-detail-readiness-save"
          onClick={() => void onAct("apply", { readiness: mode === "tcp" ? "tcp" : path })}>
          {t("pocs.detail.readiness.save")}
        </button>
      </div>
    </section>
  );
}
