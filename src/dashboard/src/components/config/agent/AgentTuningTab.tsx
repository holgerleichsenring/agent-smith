"use client";

import type { StudioAgent } from "@/lib/configApi";
import { CheckField, DrawerSection, NumberField, TextField } from "../formFields";
import { useT } from "@/hooks/useT";


// Cache, compaction and retry. Each section exists on the draft only after the operator
// adds it, so an untouched section is never persisted.

export function AgentTuningTab({
  draft,
  onChange,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
}) {
  const t = useT();
  const state = (on: boolean) => (on ? t("config.enabled") : t("config.disabled"));
  return (
    <>
      <DrawerSection
        title={t("config.tuning.cache")}
        summary={draft.cache ? state(draft.cache.isEnabled) : t("config.tuning.notSet")}
        testId="agent-section-cache"
      >
        {!draft.cache ? (
          <SectionSeed
            text={t("config.tuning.cache.none")}
            action={t("config.tuning.cache.add")}
            testId="agent-add-cache"
            onAdd={() => onChange({ ...draft, cache: { isEnabled: true, strategy: "" } })}
          />
        ) : (
          <>
            <CheckField
              label={t("config.tuning.cache.prompt")}
              value={draft.cache.isEnabled}
              testId="form-field-cache-isEnabled"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, isEnabled: v } })}
            />
            <TextField
              label={t("config.tuning.cache.strategy")}
              value={draft.cache.strategy}
              testId="form-field-cache-strategy"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, strategy: v } })}
            />
            <ClearSection testId="agent-clear-cache" onClear={() => onChange({ ...draft, cache: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title={t("config.tuning.compaction")}
        summary={draft.compaction ? state(draft.compaction.isEnabled) : t("config.tuning.notSet")}
        testId="agent-section-compaction"
      >
        {!draft.compaction ? (
          <SectionSeed
            text={t("config.tuning.compaction.none")}
            action={t("config.tuning.compaction.add")}
            testId="agent-add-compaction"
            onAdd={() =>
              onChange({
                ...draft,
                compaction: {
                  isEnabled: true,
                  thresholdIterations: 0,
                  maxContextTokens: 0,
                  keepRecentIterations: 0,
                },
              })
            }
          />
        ) : (
          <>
            <CheckField
              label={t("config.tuning.compaction.enabled")}
              value={draft.compaction.isEnabled}
              testId="form-field-compaction-isEnabled"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, isEnabled: v } })}
            />
            <NumberField
              label={t("config.tuning.compaction.threshold")}
              value={draft.compaction.thresholdIterations}
              testId="form-field-compaction-thresholdIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, thresholdIterations: v ?? 0 } })}
            />
            <NumberField
              label={t("config.tuning.compaction.maxContext")}
              value={draft.compaction.maxContextTokens}
              testId="form-field-compaction-maxContextTokens"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, maxContextTokens: v ?? 0 } })}
            />
            <NumberField
              label={t("config.tuning.compaction.keepRecent")}
              value={draft.compaction.keepRecentIterations}
              testId="form-field-compaction-keepRecentIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, keepRecentIterations: v ?? 0 } })}
            />
            {/* 2026-11-17-b34eb: the ratios; an empty field sends nothing and the stored value stays. */}
            <NumberField
              label={t("config.tuning.compaction.trigger.label")}
              value={draft.compaction.maxContextTokensTriggerRatio ?? undefined}
              step={0.05}
              help={t("config.tuning.compaction.trigger.help")}
              testId="form-field-compaction-maxContextTokensTriggerRatio"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, maxContextTokensTriggerRatio: v ?? null } })}
            />
            <NumberField
              label={t("config.tuning.compaction.foldTarget.label")}
              value={draft.compaction.foldTargetRatio ?? undefined}
              step={0.05}
              help={t("config.tuning.compaction.foldTarget.help")}
              testId="form-field-compaction-foldTargetRatio"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, foldTargetRatio: v ?? null } })}
            />
            <NumberField
              label={t("config.tuning.compaction.summaryBudget.label")}
              value={draft.compaction.summaryBudgetRatio ?? undefined}
              step={0.05}
              help={t("config.tuning.compaction.summaryBudget.help")}
              testId="form-field-compaction-summaryBudgetRatio"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, summaryBudgetRatio: v ?? null } })}
            />
            <ClearSection testId="agent-clear-compaction" onClear={() => onChange({ ...draft, compaction: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title={t("config.tuning.retry")}
        summary={draft.retry ? t("config.tuning.retry.count", { count: draft.retry.maxRetries }) : t("config.tuning.notSet")}
        testId="agent-section-retry"
      >
        {!draft.retry ? (
          <SectionSeed
            text={t("config.tuning.retry.none")}
            action={t("config.tuning.retry.add")}
            testId="agent-add-retry"
            onAdd={() =>
              onChange({
                ...draft,
                retry: { maxRetries: 3, initialDelayMs: 1000, backoffMultiplier: 2, maxDelayMs: 30000 },
              })
            }
          />
        ) : (
          <>
            <NumberField
              label={t("config.tuning.retry.max")}
              value={draft.retry.maxRetries}
              testId="form-field-retry-maxRetries"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, maxRetries: v ?? 0 } })}
            />
            <NumberField
              label={t("config.tuning.retry.initialDelay")}
              value={draft.retry.initialDelayMs}
              testId="form-field-retry-initialDelayMs"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, initialDelayMs: v ?? 0 } })}
            />
            <NumberField
              label={t("config.tuning.retry.backoff")}
              value={draft.retry.backoffMultiplier}
              testId="form-field-retry-backoffMultiplier"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, backoffMultiplier: v ?? 0 } })}
            />
            <NumberField
              label={t("config.tuning.retry.maxDelay")}
              value={draft.retry.maxDelayMs}
              testId="form-field-retry-maxDelayMs"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, maxDelayMs: v ?? 0 } })}
            />
            <ClearSection testId="agent-clear-retry" onClear={() => onChange({ ...draft, retry: undefined })} />
          </>
        )}
      </DrawerSection>
    </>
  );
}

function SectionSeed({
  text,
  action,
  onAdd,
  testId,
}: {
  text: string;
  action: string;
  onAdd: () => void;
  testId: string;
}) {
  return (
    <div className="field">
      <span className="help">{text}</span>
      <div className="picks">
        <button type="button" className="pick" data-testid={testId} onClick={onAdd}>
          {action}
        </button>
      </div>
    </div>
  );
}

function ClearSection({ onClear, testId }: { onClear: () => void; testId: string }) {
  const t = useT();
  return (
    <div className="picks">
      <button type="button" className="pick" data-testid={testId} onClick={onClear}>
        {t("config.tuning.clear")}
      </button>
    </div>
  );
}
