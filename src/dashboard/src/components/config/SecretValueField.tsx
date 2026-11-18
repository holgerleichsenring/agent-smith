"use client";

import { useCallback, useEffect, useState } from "react";
import {
  deleteSecretValue, fetchSecretValues, rewrapSecretValues, setSecretValue, type SecretValuesView,
} from "@/lib/secretValuesApi";

// 2026-11-17-f3a8d: a secret's value, set in the admin area. The field is a password input whose text goes straight
// into the request and is cleared whatever the answer; the page never holds a stored value, because the server never
// sends one. An environment variable of the same name wins over what is stored here.

export function SecretValueField({ name }: { name: string }) {
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
          {view.masterKeyProblem ?? "No master key is configured, so no value can be stored here."}
        </p>
      )}
      <p data-testid="secret-value-state" className="ec-sub">{describe(state)}</p>
      <div className="flex flex-wrap items-center gap-2">
        <input
          type="password" autoComplete="new-password" aria-label="Secret value" data-testid="secret-value-input"
          className="d-input mono max-w-md flex-1" value={value} onChange={(e) => setValue(e.target.value)}
          placeholder={state?.stored ? "enter a new value to replace it" : "enter the value"}
        />
        <button type="button" className="btn primary" data-testid="secret-value-set" disabled={value === "" || !view?.masterKeyPresent}
          onClick={() => act(() => setSecretValue(name, value))}>Set value</button>
        {state?.stored && (
          <button type="button" className="btn" data-testid="secret-value-delete" onClick={() => act(() => deleteSecretValue(name))}>
            Delete stored value
          </button>
        )}
        {state?.stored && !state.stored.keyCurrent && state.stored.readable && (
          <button type="button" className="btn" data-testid="secret-value-rewrap" onClick={() => act(rewrapSecretValues)}>
            Re-wrap with the current master key
          </button>
        )}
      </div>
      {line && <p data-testid="secret-value-line" className="ec-sub">{line}</p>}
    </div>
  );
}

function describe(state: SecretValuesView["secrets"][number] | null): string {
  if (!state) return "No value stored here yet.";
  const stored = state.stored
    ? `Stored value set by ${state.stored.setBy} at ${new Date(state.stored.setAt).toLocaleString()}`
      + (state.stored.readable ? "" : " — the running master key cannot open it")
    : "No value stored here";
  return state.fromEnvironment ? `${stored}. The environment variable ${state.name} is set and wins.` : `${stored}.`;
}
