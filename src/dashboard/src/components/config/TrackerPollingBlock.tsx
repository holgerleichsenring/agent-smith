"use client";

import type { StudioTracker } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
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
  const t = useT();
  if (!tracker.polling) {
    return (
      <div className="field">
        <label>
          {t("config.polling.label")} <span className="help">{t("config.polling.backendDefault")}</span>
        </label>
        <div className="picks">
          <button
            type="button"
            className="pick"
            data-testid="form-field-polling-add"
            onClick={() =>
              onChange({ ...tracker, polling: { enabled: true, intervalSeconds: 300 } })
            }
          >
            {t("config.polling.configure")}
          </button>
        </div>
      </div>
    );
  }
  const polling = tracker.polling;
  return (
    <>
      <CheckField
        label={t("config.polling.label")}
        value={polling.enabled}
        testId="form-field-polling-enabled"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, enabled: v } })}
      />
      <NumberField
        label={t("config.polling.interval")}
        value={polling.intervalSeconds}
        testId="form-field-polling-intervalSeconds"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, intervalSeconds: v ?? 0 } })}
      />
      <div className="picks">
        <button
          type="button"
          className="pick"
          data-testid="form-field-polling-remove"
          onClick={() => onChange({ ...tracker, polling: undefined })}
        >
          {t("config.polling.remove")}
        </button>
      </div>
    </>
  );
}
