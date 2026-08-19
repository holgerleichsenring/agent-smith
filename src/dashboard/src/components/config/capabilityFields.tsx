"use client";

import type { CapabilityField, ConfigFinding } from "@/lib/configApi";
import { TextField, ListField, CheckField, MapField } from "./formFields";

// p0345c: renders the per-TYPE field set the capabilities descriptor declares
// for the selected tracker/connection type. The field LIST comes entirely from
// the backend.
// p0392: so does the value SHAPE. The client used to hold a hardcoded set of
// "these keys are lists", which meant a backend field of any other shape could not be
// offered without editing this file — and the twelve tracker fields the descriptor did
// not declare included needs_clarification_status, whose absence refused a boot on
// 2026-07-31 and could not be fixed from the UI at all.

export function CapabilityFieldInputs({
  fields,
  values,
  onFieldChange,
  orgLabel,
  findings = [],
}: {
  fields: CapabilityField[];
  /** The entity draft, read as a loose record keyed by field key. */
  values: Record<string, unknown>;
  onFieldChange: (key: string, value: FieldValue) => void;
  /** Connection types name their org scope (organization/owner/…) — overrides
   *  the label of the `organization` field. */
  orgLabel?: string;
  /** What the server said about this draft; a finding naming a field is shown on it. */
  findings?: ConfigFinding[];
}) {
  return (
    <>
      {fields.map((f) => {
        const label = orgLabel && f.key === "organization" ? orgLabel : f.label;
        const finding = findingFor(findings, f.key);
        const help = finding?.reason;

        switch (f.kind) {
          case "list": {
            const current = Array.isArray(values[f.key]) ? (values[f.key] as string[]) : [];
            return (
              <FieldSlot key={f.key} finding={finding} fieldKey={f.key}>
                <ListField
                  label={`${label} (comma separated)`}
                  values={current}
                  testId={`form-field-${f.key}`}
                  // p0455: an emptied list travels as an empty list. The upsert reads an
                  // ABSENT list as "leave the stored value alone" (RawConfigPatch patch
                  // semantics), so dropping it to undefined saved "clear this" as
                  // "unchanged" — a list the operator could see could never be emptied.
                  onChange={(v) => onFieldChange(f.key, v)}
                />
              </FieldSlot>
            );
          }
          case "bool":
            return (
              <FieldSlot key={f.key} finding={finding} fieldKey={f.key}>
                <CheckField
                  label={label}
                  value={values[f.key] === true}
                  testId={`form-field-${f.key}`}
                  onChange={(v) => onFieldChange(f.key, v)}
                />
              </FieldSlot>
            );
          case "map": {
            const current =
              values[f.key] && typeof values[f.key] === "object"
                ? (values[f.key] as Record<string, string>)
                : {};
            return (
              <FieldSlot key={f.key} finding={finding} fieldKey={f.key}>
                <MapField
                  label={label}
                  values={current}
                  testId={`form-field-${f.key}`}
                  onChange={(v) => onFieldChange(f.key, v)}
                />
              </FieldSlot>
            );
          }
          default: {
            const current = typeof values[f.key] === "string" ? (values[f.key] as string) : "";
            return (
              <FieldSlot key={f.key} finding={finding} fieldKey={f.key}>
                <TextField
                  label={label}
                  value={current}
                  required={f.required}
                  help={help}
                  testId={`form-field-${f.key}`}
                  onChange={(v) => onFieldChange(f.key, v === "" ? undefined : v)}
                />
              </FieldSlot>
            );
          }
        }
      })}
    </>
  );
}

export type FieldValue = string | string[] | boolean | Record<string, string> | undefined;

/** Wraps a field so a server finding about it is visible ON the field, not only in a
 *  banner — an operator fixing six things needs six markers, each next to its input. */
function FieldSlot({
  finding,
  fieldKey,
  children,
}: {
  finding: ConfigFinding | undefined;
  fieldKey: string;
  children: React.ReactNode;
}) {
  if (!finding) return <>{children}</>;
  return (
    <div data-testid={`form-finding-${fieldKey}`} data-severity={finding.severity}>
      {children}
      <p className="help" style={{ color: finding.severity === "blocking" ? "var(--bad)" : undefined }}>
        {finding.reason}
      </p>
    </div>
  );
}

/** The server names findings by the YAML field (needs_clarification_status); the form
 *  keys them camelCase. One conversion, in one place. */
export function findingFor(findings: ConfigFinding[], key: string): ConfigFinding | undefined {
  return findings.find((f) => f.field && camelCase(f.field) === key);
}

export function camelCase(yamlKey: string): string {
  return yamlKey.replace(/_([a-z])/g, (_, c: string) => c.toUpperCase());
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
      if (typeof v === "boolean") return true;
      return typeof v === "string" && v.trim().length > 0;
    });
}
