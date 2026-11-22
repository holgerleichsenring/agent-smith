"use client";

import type { ConfigCapabilities, StudioAgent } from "@/lib/configApi";
import type { ConfigCatalog } from "../useConfigCatalog";
import { NumberField, RefSelect, SelectField, TextField } from "../formFields";
import { useT } from "@/hooks/useT";

// The agent's own connection: provider, key, endpoint, api version, timeout, runs at a time (2026-11-20-24bd), the
// per-minute budget (2026-11-22-66e4b; its texts read from the dictionary, the older fields move over in 5485b). A catalog
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
  const t = useT();
  // 2026-11-22-66e4b: one number may stay empty (the provider default); both empty writes no section.
  const budget = (next: { requestsPerMinute?: number | null; inputTokensPerMinute?: number | null }) => {
    const merged = { requestsPerMinute: draft.rateLimit?.requestsPerMinute ?? null,
      inputTokensPerMinute: draft.rateLimit?.inputTokensPerMinute ?? null, ...next };
    const empty = merged.requestsPerMinute == null && merged.inputTokensPerMinute == null;
    onChange({ ...draft, rateLimit: empty ? null : merged });
  };
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
      <NumberField
        label="runs at a time on this model"
        value={draft.maxConcurrentRuns ?? undefined}
        placeholder="no limit"
        help="max_concurrent_runs: a run and all its sub-agents count once; other runs wait. Set 1 for a shared deployment whose quota one run fills."
        testId="form-field-maxConcurrentRuns"
        onChange={(v) => onChange({ ...draft, maxConcurrentRuns: v ?? null })}
      />
      <NumberField
        label={t("config.agent.rateLimit.tokens")}
        value={draft.rateLimit?.inputTokensPerMinute ?? undefined}
        placeholder={t("config.agent.rateLimit.default")}
        help={t("config.agent.rateLimit.tokensHelp")}
        testId="form-field-rateLimit-inputTokensPerMinute"
        onChange={(v) => budget({ inputTokensPerMinute: v ?? null })}
      />
      <NumberField
        label={t("config.agent.rateLimit.requests")}
        value={draft.rateLimit?.requestsPerMinute ?? undefined}
        placeholder={t("config.agent.rateLimit.default")}
        help={t("config.agent.rateLimit.requestsHelp")}
        testId="form-field-rateLimit-requestsPerMinute"
        onChange={(v) => budget({ requestsPerMinute: v ?? null })}
      />
    </>
  );
}
