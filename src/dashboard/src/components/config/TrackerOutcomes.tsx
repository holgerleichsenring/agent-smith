"use client";

import type { CapabilityField, ConfigFinding } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { CapabilityFieldInputs, type FieldValue } from "./capabilityFields";

// 2026-10-06-cea8: the four statuses a run can leave a ticket in, read as one decision —
// "when a run ends this way, the ticket moves there". Two outcomes on one status is legal and
// often a mistake: a board where done and failed both read "In Review" cannot tell them apart.

export function TrackerOutcomes({
  fields,
  values,
  onFieldChange,
  findings,
}: {
  fields: CapabilityField[];
  values: Record<string, unknown>;
  onFieldChange: (key: string, value: FieldValue) => void;
  findings: ConfigFinding[];
}) {
  const t = useT();
  const shared = sharedStatuses(fields, values);
  return (
    <div className="trk-outcomes" data-testid="tracker-outcomes">
      <span className="trk-outcomes-head">{t("config.tracker.outcomes.head")}</span>
      <CapabilityFieldInputs
        fields={fields}
        values={values}
        onFieldChange={onFieldChange}
        findings={findings}
      />
      {shared.map(({ status, labels }) => (
        <p className="trk-warn" key={status} data-testid="tracker-outcomes-shared">
          {listed(t, labels)} <b>{status}</b> {t("config.tracker.outcomes.cannotTell")}
        </p>
      ))}
    </div>
  );
}

/** Non-empty values that two or more outcome fields share, compared without case. */
export function sharedStatuses(
  fields: CapabilityField[],
  values: Record<string, unknown>,
): { status: string; labels: string[] }[] {
  const byStatus = new Map<string, { status: string; labels: string[] }>();
  for (const f of fields) {
    const raw = values[f.key];
    if (typeof raw !== "string" || raw.trim() === "") continue;
    const key = raw.trim().toLowerCase();
    const entry = byStatus.get(key) ?? { status: raw.trim(), labels: [] };
    entry.labels.push(f.label);
    byStatus.set(key, entry);
  }
  return [...byStatus.values()].filter((e) => e.labels.length > 1);
}

function listed(t: T, labels: string[]): string {
  return labels.length === 2
    ? t("config.tracker.outcomes.sharedTwo", { first: labels[0], second: labels[1] })
    : t("config.tracker.outcomes.sharedMany", { list: labels.slice(0, -1).join(", "), last: labels[labels.length - 1] });
}
