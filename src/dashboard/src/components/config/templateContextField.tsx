"use client";

import { SelectField, TextField } from "./formFields";
import type { useProjectContexts } from "./useProjectContexts";

// 2026-09-14-620e: a context name is PICKED while there is a list and TYPED when there is
// not. 2026-09-15-a2d0 moved both helpers here, because the field-level read and the
// editor's own read both want them and neither owns the other.

/** Where a context list comes from, said on the field rather than left to be assumed. */
const FROM_DEFAULT_BRANCH = "read from the repository's default branch";

/** Why a list is missing, never instead of the field — the name stays typeable. */
export function Unreadable({
  lines,
  subject,
  testId,
}: {
  lines: string[];
  subject: string;
  testId: string;
}) {
  if (lines.length === 0) return null;
  return (
    <span className="help" data-testid={`${testId}-unreadable`}>
      could not read {subject}: {lines.join("; ")}
    </span>
  );
}

/**
 * The fallback is the whole point of the FIELD degrading rather than the form — an
 * unreachable target is not a reason an operator cannot finish editing a project, and a
 * select with no options would be exactly that.
 */
export function ContextField({
  label,
  value,
  state,
  subject,
  testId,
  onChange,
}: {
  label: string;
  value: string;
  state: ReturnType<typeof useProjectContexts>;
  subject: string;
  testId: string;
  onChange: (v: string) => void;
}) {
  const shared = state.origins.filter((o) => o.repos.length > 1);
  const help = state.loading
    ? `reading ${subject}...`
    : state.origins.length > 0
      ? shared.length > 0
        // The stored reference is a bare name, and the run keys its scopes by that bare
        // name — so a name two repositories declare opens one scope both match. Said here
        // because the picker cannot resolve it and must not pretend to.
        ? `${FROM_DEFAULT_BRANCH} — ${shared.map((o) => o.name).join(", ")} declared by more than one`
        : FROM_DEFAULT_BRANCH
      : state.unreadable.length > 0
        ? "no list to pick from - type the name"
        : `${subject} declares no context - type the name`;

  return state.origins.length > 0 ? (
    <SelectField
      label={label}
      value={value}
      // One option per distinct NAME. Two options sharing a value would be a choice that is
      // not one; the repositories ride the label instead.
      options={state.origins.map((o) => ({ value: o.name, label: `${o.name}  ·  ${o.repos.join(", ")}` }))}
      help={help}
      testId={testId}
      onChange={onChange}
    />
  ) : (
    <TextField label={label} value={value} mono help={help} testId={testId} onChange={onChange} />
  );
}
