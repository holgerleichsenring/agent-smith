"use client";

import type { ConfigCapabilities, StudioAgent } from "@/lib/configApi";
import type { ConfigCatalog } from "../useConfigCatalog";
import { NumberField, RefSelect, SelectField, TextField } from "../formFields";

// The agent's own connection: provider, key, endpoint, api version, timeout. A catalog
// entry answers here unless it names a provider or endpoint of its own.

export function AgentProviderTab({
  draft,
  onChange,
  catalog,
  capabilities,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
}) {
  return (
    <>
      <SelectField
        label="provider"
        value={draft.provider}
        options={capabilities?.agentProviders ?? []}
        required
        help={capabilities ? undefined : "capabilities unavailable"}
        testId="form-field-provider"
        onChange={(v) => onChange({ ...draft, provider: v })}
      />
      <RefSelect
        label="key secret"
        value={draft.keySecret ?? ""}
        options={catalog.secrets}
        testId="form-ref-keySecret"
        onChange={(v) => onChange({ ...draft, keySecret: v || null })}
      />
      <TextField
        label="endpoint"
        value={draft.endpoint ?? ""}
        mono
        testId="form-field-endpoint"
        placeholder="https://…"
        onChange={(v) => onChange({ ...draft, endpoint: v || undefined })}
      />
      <TextField
        label="api version"
        value={draft.apiVersion ?? ""}
        mono
        testId="form-field-apiVersion"
        onChange={(v) => onChange({ ...draft, apiVersion: v || undefined })}
      />
      <NumberField
        label="network timeout (seconds)"
        value={draft.networkTimeoutSeconds}
        testId="form-field-networkTimeoutSeconds"
        onChange={(v) => onChange({ ...draft, networkTimeoutSeconds: v })}
      />
    </>
  );
}
