"use client";

import type { StudioTracker } from "@/lib/configApi";
import { CheckField, NumberField } from "./formFields";

// p0345c: polling is part of the v2 tracker CONTRACT (not per-type) — an
// optional section: absent until the operator adds it, then enabled/interval/
// jitter are editable; removing it restores the backend default.
export function TrackerPollingBlock({
  tracker,
  onChange,
}: {
  tracker: StudioTracker;
  onChange: (next: StudioTracker) => void;
}) {
  if (!tracker.polling) {
    return (
      <div className="field">
        <label>
          polling <span className="help">backend default applies</span>
        </label>
        <div className="picks">
          <button
            type="button"
            className="pick"
            data-testid="form-field-polling-add"
            onClick={() =>
              onChange({ ...tracker, polling: { enabled: true, intervalSeconds: 300, jitterPercent: 10 } })
            }
          >
            Configure polling
          </button>
        </div>
      </div>
    );
  }
  const polling = tracker.polling;
  return (
    <>
      <CheckField
        label="polling"
        value={polling.enabled}
        testId="form-field-polling-enabled"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, enabled: v } })}
      />
      <NumberField
        label="poll interval (seconds)"
        value={polling.intervalSeconds}
        testId="form-field-polling-intervalSeconds"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, intervalSeconds: v ?? 0 } })}
      />
      <NumberField
        label="jitter (%)"
        value={polling.jitterPercent}
        testId="form-field-polling-jitterPercent"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, jitterPercent: v ?? 0 } })}
      />
      <div className="picks">
        <button
          type="button"
          className="pick"
          data-testid="form-field-polling-remove"
          onClick={() => onChange({ ...tracker, polling: undefined })}
        >
          Remove polling override
        </button>
      </div>
    </>
  );
}
