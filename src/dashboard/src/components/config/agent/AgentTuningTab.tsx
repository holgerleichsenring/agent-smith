"use client";

import type { StudioAgent } from "@/lib/configApi";
import { CheckField, DrawerSection, NumberField, TextField } from "../formFields";

// Cache, compaction and retry. Each section exists on the draft only after the operator
// adds it, so an untouched section is never persisted.

export function AgentTuningTab({
  draft,
  onChange,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
}) {
  return (
    <>
      <DrawerSection
        title="Cache"
        summary={draft.cache ? (draft.cache.isEnabled ? "enabled" : "disabled") : "not set"}
        testId="agent-section-cache"
      >
        {!draft.cache ? (
          <SectionSeed
            text="No cache settings — the backend default applies."
            action="Add cache settings"
            testId="agent-add-cache"
            onAdd={() => onChange({ ...draft, cache: { isEnabled: true, strategy: "" } })}
          />
        ) : (
          <>
            <CheckField
              label="prompt caching"
              value={draft.cache.isEnabled}
              testId="form-field-cache-isEnabled"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, isEnabled: v } })}
            />
            <TextField
              label="strategy"
              value={draft.cache.strategy}
              testId="form-field-cache-strategy"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, strategy: v } })}
            />
            <ClearSection testId="agent-clear-cache" onClear={() => onChange({ ...draft, cache: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title="Compaction"
        summary={draft.compaction ? (draft.compaction.isEnabled ? "enabled" : "disabled") : "not set"}
        testId="agent-section-compaction"
      >
        {!draft.compaction ? (
          <SectionSeed
            text="No compaction settings — the backend default applies."
            action="Add compaction settings"
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
              label="compaction"
              value={draft.compaction.isEnabled}
              testId="form-field-compaction-isEnabled"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, isEnabled: v } })}
            />
            <NumberField
              label="threshold iterations"
              value={draft.compaction.thresholdIterations}
              testId="form-field-compaction-thresholdIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, thresholdIterations: v ?? 0 } })}
            />
            <NumberField
              label="max context tokens"
              value={draft.compaction.maxContextTokens}
              testId="form-field-compaction-maxContextTokens"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, maxContextTokens: v ?? 0 } })}
            />
            <NumberField
              label="keep recent iterations"
              value={draft.compaction.keepRecentIterations}
              testId="form-field-compaction-keepRecentIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, keepRecentIterations: v ?? 0 } })}
            />
            <ClearSection testId="agent-clear-compaction" onClear={() => onChange({ ...draft, compaction: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title="Retry"
        summary={draft.retry ? `${draft.retry.maxRetries} retries` : "not set"}
        testId="agent-section-retry"
      >
        {!draft.retry ? (
          <SectionSeed
            text="No retry policy — the backend default applies."
            action="Add retry policy"
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
              label="max retries"
              value={draft.retry.maxRetries}
              testId="form-field-retry-maxRetries"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, maxRetries: v ?? 0 } })}
            />
            <NumberField
              label="initial delay (ms)"
              value={draft.retry.initialDelayMs}
              testId="form-field-retry-initialDelayMs"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, initialDelayMs: v ?? 0 } })}
            />
            <NumberField
              label="backoff multiplier"
              value={draft.retry.backoffMultiplier}
              testId="form-field-retry-backoffMultiplier"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, backoffMultiplier: v ?? 0 } })}
            />
            <NumberField
              label="max delay (ms)"
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
  return (
    <div className="picks">
      <button type="button" className="pick" data-testid={testId} onClick={onClear}>
        Remove section (use backend default)
      </button>
    </div>
  );
}
