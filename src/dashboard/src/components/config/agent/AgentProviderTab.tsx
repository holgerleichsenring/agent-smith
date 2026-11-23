"use client";

import type { ConfigCapabilities, StudioAgent } from "@/lib/configApi";
import type { ConfigCatalog } from "../useConfigCatalog";
import { NumberField, RefSelect, SelectField, TextField } from "../formFields";
import { useT } from "@/hooks/useT";

// The agent's own connection: provider, key, endpoint, api version, timeout, runs at a time (2026-11-20-24bd), the
// per-minute budget (2026-11-22-66e4b). A catalog
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
        label={t("config.agent.provider")}
        value={draft.provider}
        options={capabilities?.agentProviders ?? []}
        required
        help={capabilities ? undefined : t("config.capabilitiesUnavailable")}
        testId="form-field-provider"
        onChange={(v) => onChange({ ...draft, provider: v })}
      />
      <RefSelect
        label={t("config.agent.keySecret")}
        value={draft.keySecret ?? ""}
        options={catalog.secrets}
        testId="form-ref-keySecret"
        onChange={(v) => onChange({ ...draft, keySecret: v || null })}
      />
      <TextField
        label={t("config.agent.endpoint")}
        value={draft.endpoint ?? ""}
        mono
        testId="form-field-endpoint"
        placeholder="https://…"
        onChange={(v) => onChange({ ...draft, endpoint: v || undefined })}
      />
      <TextField
        label={t("config.agent.apiVersion")}
        value={draft.apiVersion ?? ""}
        mono
        testId="form-field-apiVersion"
        onChange={(v) => onChange({ ...draft, apiVersion: v || undefined })}
      />
      <NumberField
        label={t("config.agent.networkTimeout")}
        value={draft.networkTimeoutSeconds}
        testId="form-field-networkTimeoutSeconds"
        onChange={(v) => onChange({ ...draft, networkTimeoutSeconds: v })}
      />
      <NumberField
        label={t("config.agent.maxConcurrentRuns.label")}
        value={draft.maxConcurrentRuns ?? undefined}
        placeholder={t("config.agent.maxConcurrentRuns.placeholder")}
        help={t("config.agent.maxConcurrentRuns.help")}
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
