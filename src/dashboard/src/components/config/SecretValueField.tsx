"use client";

import { useCallback, useEffect, useState } from "react";
import {
  deleteSecretValue, fetchSecretValues, rewrapSecretValues, setSecretValue, type SecretValuesView,
} from "@/lib/secretValuesApi";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

// 2026-11-17-f3a8d: a secret's value, set in the admin area. The field is a password input whose text goes straight
// into the request and is cleared whatever the answer; the page never holds a stored value, because the server never
// sends one. An environment variable of the same name wins over what is stored here.

export function SecretValueField({ name }: { name: string }) {
  const t = useT();
  const locale = useDateLocale();
  const [view, setView] = useState<SecretValuesView | null>(null);
  const [value, setValue] = useState("");
  const [line, setLine] = useState<string | null>(null);
  const reload = useCallback(() => {
    fetchSecretValues().then(setView).catch((e: Error) => setLine(e.message));
  }, []);
  useEffect(reload, [reload]);
  const state = view?.secrets.find((s) => s.name.toLowerCase() === name.toLowerCase()) ?? null;

  const act = (run: () => Promise<{ line: string }>) => {
    run().then((r) => setLine(r.line)).catch((e: Error) => setLine(e.message)).finally(() => {
      setValue("");
      reload();
    });
  };

  return (
    <div data-testid="secret-value-field" className="flex flex-col gap-2">
      {view && !view.masterKeyPresent && (
        <p data-testid="secret-value-no-key" className="dsh-body text-ink">
          {view.masterKeyProblem ?? t("config.secret.newValue.noKey")}
        </p>
      )}
      <p data-testid="secret-value-state" className="ec-sub">{describe(t, locale, state)}</p>
      {view?.masterKeySource && (
        <p data-testid="secret-value-key-source" className="ec-sub">{t("config.secret.keySource", { source: view.masterKeySource })}</p>
      )}
      <div className="flex flex-wrap items-center gap-2">
        <input
          type="password" autoComplete="new-password" aria-label={t("config.secret.valueAria")} data-testid="secret-value-input"
          className="d-input mono max-w-md flex-1" value={value} onChange={(e) => setValue(e.target.value)}
          placeholder={state?.stored ? t("config.secret.replacePlaceholder") : t("config.secret.newValue.placeholder")}
        />
        <button type="button" className="btn primary" data-testid="secret-value-set" disabled={value === "" || !view?.masterKeyPresent}
          onClick={() => act(() => setSecretValue(name, value))}>{t("config.secret.set")}</button>
        {state?.stored && (
          <button type="button" className="btn" data-testid="secret-value-delete" onClick={() => act(() => deleteSecretValue(name))}>
            {t("config.secret.deleteStored")}
          </button>
        )}
        {state?.stored && !state.stored.keyCurrent && state.stored.readable && (
          <button type="button" className="btn" data-testid="secret-value-rewrap" onClick={() => act(rewrapSecretValues)}>
            {t("config.secret.rewrap")}
          </button>
        )}
      </div>
      {line && <p data-testid="secret-value-line" className="ec-sub">{line}</p>}
    </div>
  );
}

// 2026-11-23-78d7: a new secret's value, typed before the secret exists. The drawer holds it outside the entity draft
// and sends it with Create through the same secret run; nothing here talks to the server but the master-key check.
export function NewSecretValueInput({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const t = useT();
  const [keyPresent, setKeyPresent] = useState<boolean | null>(null);
  useEffect(() => {
    const ctl = new AbortController();
    fetchSecretValues(ctl.signal).then((v) => setKeyPresent(v.masterKeyPresent)).catch(() => setKeyPresent(null));
    return () => ctl.abort();
  }, []);
  return (
    <div data-testid="secret-new-value" className="flex flex-col gap-2">
      <label className="dsh-label" htmlFor="secret-new-value-input">{t("config.secret.newValue.label")}</label>
      <input
        id="secret-new-value-input" type="password" autoComplete="new-password" data-testid="secret-new-value-input"
        className="d-input mono max-w-md" value={value} disabled={keyPresent === false}
        placeholder={t("config.secret.newValue.placeholder")} onChange={(e) => onChange(e.target.value)}
      />
      <p className="ec-sub" data-testid="secret-new-value-help">
        {keyPresent === false ? t("config.secret.newValue.noKey") : t("config.secret.newValue.help")}
      </p>
    </div>
  );
}

function describe(t: T, locale: string, state: SecretValuesView["secrets"][number] | null): string {
  if (!state) return t("config.secret.state.noneYet");
  const stored = state.stored
    ? t(state.stored.readable ? "config.secret.state.stored" : "config.secret.state.storedUnreadable", {
      by: state.stored.setBy, at: new Date(state.stored.setAt).toLocaleString(locale),
    })
    : t("config.secret.state.none");
  return state.fromEnvironment
    ? t("config.secret.state.environmentWins", { stored, variable: state.variable })
    : t("config.secret.state.sentence", { stored });
}
