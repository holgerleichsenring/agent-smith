"use client";

import { useT } from "@/hooks/useT";
import type { PocVariableInput } from "@/lib/pocsApi";

// 2026-11-23-933dg: a PoC's variables as rows of name, kind and value. A secret value is a password field and is sealed
// for the cluster; a plain value is committed in plain text as a ConfigMap. The hints below a row are the client's
// reading of the server's rules — the server decides. Controlled: the import page holds the rows and clears the secret
// values once it has posted them; 2026-11-23-c81ed mounts the same table on the PoC detail page.

export const VARIABLE_NAME = /^[A-Z_][A-Z0-9_]*$/;
export const VARIABLE_NAME_MAX_CHARS = 200;
export const SECRET_VALUE_MIN_CHARS = 6;

export function emptyVariable(): PocVariableInput {
  return { name: "", secret: true, value: "" };
}

/** The rows with every secret value emptied: a secret never stays in a form after it was sent. */
export function withoutSecretValues(rows: PocVariableInput[]): PocVariableInput[] {
  return rows.map((row) => (row.secret ? { ...row, value: "" } : row));
}

/** The rows worth sending: those with a name. */
export function variablesToSend(rows: PocVariableInput[]): PocVariableInput[] {
  return rows.filter((row) => row.name.trim() !== "").map((row) => ({ ...row, name: row.name.trim() }));
}

export function PocVariablesTable({ rows, onChange, disabled, testId = "poc-variables" }: {
  rows: PocVariableInput[]; onChange: (rows: PocVariableInput[]) => void; disabled?: boolean; testId?: string;
}) {
  const t = useT();
  const setAt = (i: number, patch: Partial<PocVariableInput>) =>
    onChange(rows.map((row, j) => (j === i ? { ...row, ...patch } : row)));
  return (
    <section className="poc-box poc-variables" data-testid={testId}>
      <h2>{t("pocs.variables.title")}</h2>
      <p className="poc-meta" data-testid={`${testId}-note`}>{t("pocs.variables.note")}</p>
      {rows.length > 0 && (
        <table className="poc-variables-table">
          <thead>
            <tr>
              <th>{t("pocs.variables.name")}</th>
              <th>{t("pocs.variables.secret")}</th>
              <th>{t("pocs.variables.value")}</th>
              <th aria-label={t("pocs.variables.remove")} />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => {
              const id = `${testId}-row-${i}`;
              const hints = hintsFor(row);
              return (
                <tr key={i} data-testid={id}>
                  <td>
                    <input type="text" className="mono" value={row.name} spellCheck={false} autoComplete="off" disabled={disabled}
                      aria-label={t("pocs.variables.name")} placeholder="DATABASE_URL" data-testid={`${id}-name`}
                      onChange={(e) => setAt(i, { name: e.target.value })} />
                  </td>
                  <td>
                    <input type="checkbox" checked={row.secret} disabled={disabled} aria-label={t("pocs.variables.secret")}
                      data-testid={`${id}-secret`} onChange={(e) => setAt(i, { secret: e.target.checked })} />
                  </td>
                  <td>
                    <input type={row.secret ? "password" : "text"} className="mono" value={row.value} spellCheck={false}
                      autoComplete={row.secret ? "new-password" : "off"} disabled={disabled} aria-label={t("pocs.variables.value")}
                      data-testid={`${id}-value`} onChange={(e) => setAt(i, { value: e.target.value })} />
                    {hints.map((hint) => <span key={hint} className="poc-meta" data-testid={`${id}-hint`}>{t(hint)}</span>)}
                  </td>
                  <td>
                    <button type="button" className="btn" disabled={disabled} data-testid={`${id}-remove`}
                      onClick={() => onChange(rows.filter((_, j) => j !== i))}>
                      {t("pocs.variables.remove")}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
      <div className="poc-bar">
        <button type="button" className="btn" disabled={disabled} data-testid={`${testId}-add`}
          onClick={() => onChange([...rows, emptyVariable()])}>
          {t("pocs.variables.add")}
        </button>
      </div>
    </section>
  );
}

type Hint = "pocs.variables.hint.name" | "pocs.variables.hint.nameLength" | "pocs.variables.hint.secretShort";

function hintsFor(row: PocVariableInput): Hint[] {
  const hints: Hint[] = [];
  const name = row.name.trim();
  if (name && !VARIABLE_NAME.test(name)) hints.push("pocs.variables.hint.name");
  if (name.length > VARIABLE_NAME_MAX_CHARS) hints.push("pocs.variables.hint.nameLength");
  if (row.secret && row.value !== "" && row.value.length < SECRET_VALUE_MIN_CHARS) hints.push("pocs.variables.hint.secretShort");
  return hints;
}
