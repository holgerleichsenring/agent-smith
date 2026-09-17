"use client";

import { useEffect } from "react";
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
 * 2026-09-16-4df5: the LOCAL context picker. One option per repository-and-name PAIR, so a
 * project whose Client and BackgroundWorker both declare "default" offers two choices and
 * each can be bound to its own template.
 * <para>
 * A select emits one string and the stored context is a bare name, so the option value
 * encodes the pair and the handler decodes it and writes both fields — on every pick, because
 * choosing a pair is choosing a context OF that repository. The TARGET context
 * picker keeps the plain-string shape below: it asks about exactly one repository, so it
 * cannot collide.
 * </para>
 */
const SEP = "\u0000";
const encode = (name: string, repo: string | null) => (repo ? `${repo}${SEP}${name}` : name);
const decode = (v: string): { name: string; repo: string | null } => {
  const at = v.indexOf(SEP);
  return at < 0 ? { name: v, repo: null } : { name: v.slice(at + 1), repo: v.slice(0, at) };
};

export function LocalContextField({
  value,
  contextRepo,
  state,
  testId,
  onChange,
}: {
  value: string;
  contextRepo: string | null;
  state: ReturnType<typeof useProjectContexts>;
  testId: string;
  onChange: (name: string, repo: string | null) => void;
}) {
  const subject = "this project's repositories";
  // A stored name with no repository that exactly one repository declares has an origin,
  // not a guess: fill it in once the list arrives, so the draft changes and one save places
  // it. A name two repositories declare stays as stored for the operator to pick again.
  const sole = contextRepo || !value ? null : state.origins.find((o) => o.name === value);
  const fill = sole && sole.repos.length === 1 ? sole.repos[0] : null;
  useEffect(() => {
    if (fill) onChange(value, fill);
    // onChange is a fresh closure on every render; the fill itself is what may trigger this.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fill, value]);

  if (state.origins.length === 0)
    return (
      <TextField
        label="context"
        value={value}
        mono
        help={
          state.loading
            ? `reading ${subject}...`
            : state.unreadable.length > 0
              ? "no list to pick from - type the name"
              : `${subject} declare no context - type the name`
        }
        testId={testId}
        onChange={(v) => onChange(v, null)}
      />
    );

  // One option per pair, and every value carries its repository — the graph places a context
  // by the repository stored with it, and draws from nothing else.
  const options = state.origins.flatMap((o) =>
    o.repos.map((repo) => ({
      value: encode(o.name, repo),
      label: `${o.name}  ·  ${repo}`,
    })),
  );

  return (
    <SelectField
      label="context"
      value={encode(value, contextRepo)}
      options={options}
      help={FROM_DEFAULT_BRANCH}
      testId={testId}
      onChange={(v) => {
        const picked = decode(v);
        onChange(picked.name, picked.repo);
      }}
    />
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
