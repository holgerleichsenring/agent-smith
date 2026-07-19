"use client";

import type { CapabilityField } from "@/lib/configApi";
import { TextField, ListField } from "./formFields";

// p0345c: renders the per-TYPE field set the capabilities descriptor declares
// for the selected tracker/connection type. The field LIST comes entirely from
// the backend; the only client knowledge is the v2 entity contract's value
// shapes — which keys are string lists rather than scalars.

const LIST_FIELD_KEYS = new Set(["openStates", "triggerStatuses"]);

export function CapabilityFieldInputs({
  fields,
  values,
  onFieldChange,
  orgLabel,
}: {
  fields: CapabilityField[];
  /** The entity draft, read as a loose record keyed by field key. */
  values: Record<string, unknown>;
  onFieldChange: (key: string, value: string | string[] | undefined) => void;
  /** Connection types name their org scope (organization/owner/…) — overrides
   *  the label of the `organization` field. */
  orgLabel?: string;
}) {
  return (
    <>
      {fields.map((f) => {
        const label = orgLabel && f.key === "organization" ? orgLabel : f.label;
        if (LIST_FIELD_KEYS.has(f.key)) {
          const current = Array.isArray(values[f.key]) ? (values[f.key] as string[]) : [];
          return (
            <ListField
              key={f.key}
              label={`${label} (comma separated)`}
              values={current}
              testId={`form-field-${f.key}`}
              onChange={(v) => onFieldChange(f.key, v.length > 0 ? v : undefined)}
            />
          );
        }
        const current = typeof values[f.key] === "string" ? (values[f.key] as string) : "";
        return (
          <TextField
            key={f.key}
            label={label}
            value={current}
            required={f.required}
            testId={`form-field-${f.key}`}
            onChange={(v) => onFieldChange(f.key, v === "" ? undefined : v)}
          />
        );
      })}
    </>
  );
}

/** Switching type prunes per-type fields the NEW type does not declare —
 *  keys shared between types survive, foreign leftovers do not linger. */
export function pruneToType<T extends { type: string }>(
  entity: T,
  descriptors: { type: string; fields: CapabilityField[] }[],
  nextType: string,
): T {
  const allKeys = new Set(descriptors.flatMap((d) => d.fields.map((f) => f.key)));
  const keep = new Set(descriptors.find((d) => d.type === nextType)?.fields.map((f) => f.key) ?? []);
  const next = { ...entity, type: nextType } as Record<string, unknown>;
  for (const key of allKeys) if (!keep.has(key)) delete next[key];
  return next as T;
}

/** Are all required per-type fields of the descriptor filled on the draft? */
export function requiredFieldsFilled(
  fields: CapabilityField[],
  values: Record<string, unknown>,
): boolean {
  return fields
    .filter((f) => f.required)
    .every((f) => {
      const v = values[f.key];
      if (Array.isArray(v)) return v.length > 0;
      return typeof v === "string" && v.trim().length > 0;
    });
}
