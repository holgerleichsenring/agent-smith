"use client";

import { useRef, useState } from "react";
import { useT } from "@/hooks/useT";

// 2026-11-25-9992d: sandbox.secrets.repo_env — variables only one repository's commands receive. Each row is a
// repository, a variable NAME and its "secretName:key" reference; the rows fold into repo -> NAME -> reference. Like
// SecretFileRows it edits names: the value lives in the cluster.

export type RepoSecretEnv = Record<string, Record<string, string>>;

type Row = { id: number; repo: string; name: string; ref: string };

const FIELDS = [
  { key: "repo", label: "config.sandboxSecrets.repoEnv.repo", placeholder: "my-api" },
  { key: "name", label: "config.sandboxSecrets.repoEnv.name", placeholder: "DB_PASSWORD" },
  { key: "ref", label: "config.sandboxSecrets.repoEnv.ref", placeholder: "my-api-creds:db-password" },
] as const;

function toRows(value: RepoSecretEnv | null | undefined): Row[] {
  return Object.entries(value ?? {}).flatMap(([repo, vars]) =>
    Object.entries(vars ?? {}).map(([name, ref]) => ({ repo, name, ref })),
  ).map((r, id) => ({ ...r, id }));
}

/** Folds complete rows into the map; a row missing its repository or name is still being typed and is left out. */
export function foldRepoEnv(rows: readonly Omit<Row, "id">[]): RepoSecretEnv | undefined {
  const folded: RepoSecretEnv = {};
  for (const { repo, name, ref } of rows) {
    if (repo.trim() === "" || name.trim() === "") continue;
    (folded[repo.trim()] ??= {})[name.trim()] = ref.trim();
  }
  return Object.keys(folded).length > 0 ? folded : undefined;
}

export function RepoSecretEnvRows({
  values,
  onChange,
  testId,
}: {
  values: RepoSecretEnv | null | undefined;
  onChange: (v: RepoSecretEnv | undefined) => void;
  testId: string;
}) {
  const t = useT();
  const [rows, setRows] = useState<Row[]>(() => toRows(values));
  const nextId = useRef(rows.length);
  const label = t("config.sandboxSecrets.repoEnv.label");

  const apply = (next: Row[]) => {
    setRows(next);
    onChange(foldRepoEnv(next));
  };
  const edit = (id: number, patch: Partial<Row>) => apply(rows.map((r) => (r.id === id ? { ...r, ...patch } : r)));

  return (
    <div className="field" data-testid={testId}>
      <label>
        {label} <span className="help">{t("config.sandboxSecrets.repoEnv.help")}</span>
      </label>
      <div className="maprows">
        {rows.map((row, i) => (
          <div className="maprow" key={row.id}>
            {FIELDS.map((f) => (
              <input key={f.key} type="text" className="mono" aria-label={`${label} ${t(f.label)} ${i + 1}`}
                data-testid={`${testId}-${f.key}-${i}`} placeholder={f.placeholder} value={row[f.key]}
                onChange={(e) => edit(row.id, { [f.key]: e.target.value })} />
            ))}
            <button type="button" className="pick" aria-label={t("config.removeRow", { label, n: i + 1 })}
              data-testid={`${testId}-remove-${i}`} onClick={() => apply(rows.filter((r) => r.id !== row.id))}>
              ×
            </button>
          </div>
        ))}
        {rows.length === 0 && <span className="help">{t("config.sandboxSecrets.repoEnv.none")}</span>}
      </div>
      <button type="button" className="pick" data-testid={`${testId}-add`}
        onClick={() => setRows([...rows, { id: nextId.current++, repo: "", name: "", ref: "" }])}>
        {t("config.sandboxSecrets.repoEnv.add")}
      </button>
    </div>
  );
}
