"use client";

import type { SandboxSecrets, SandboxSecretFile } from "@/lib/configApi";
import { MapField } from "./formFields";
import { SecretFileRows } from "./SecretFileRows";

// 2026-09-22-6c46: the pod's secret injection. It has NO process-wide counterpart — the
// global sandbox block carries no secrets field — so it inherits NOTHING, and a placeholder
// here would be a lie: a blank field would read as "an inherited empty set" rather than
// "there is nothing to inherit". The block says so instead.
//
// It edits NAMES: a Kubernetes Secret and the keys taken from it. The values live in the
// cluster, and the only reason an env row has two boxes is that its value IS a reference —
// "secretName:key" — which Kubernetes resolves when it builds the pod.

export function SandboxSecretsBlock({
  value,
  onChange,
}: {
  value: SandboxSecrets | null | undefined;
  onChange: (next: SandboxSecrets | undefined) => void;
}) {
  const secrets = value ?? {};
  const env = secrets.env ?? {};
  const files = secrets.files ?? [];

  const emit = (next: SandboxSecrets) =>
    onChange(
      (next.env && Object.keys(next.env).length > 0) || (next.files && next.files.length > 0)
        ? next
        : undefined,
    );

  return (
    <div data-testid="form-field-sandbox-secrets">
      <p className="help" data-testid="form-sandbox-secrets-none-inherited">
        Nothing is inherited here: there is no process-wide secrets block to fall back to, so
        this project declares these or it has none. Names only — the values stay in the
        cluster and this product never sees one.
      </p>

      <MapField
        label="secret environment variables"
        values={env}
        testId="form-field-sandbox-secrets-env"
        help="environment variable name, and the secretName:key it is taken from"
        onChange={(next) => emit({ ...secrets, env: next ?? null })}
      />

      <SecretFileRows
        label="mounted secret files"
        values={files as readonly SandboxSecretFile[]}
        testId="form-field-sandbox-secrets-files"
        help="mount path, the secret holding the value, and the key projected as the file"
        onChange={(next) => emit({ ...secrets, files: next ?? null })}
      />
    </div>
  );
}
