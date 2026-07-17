"use client";

import { cn } from "@/lib/utils";
import type { StudioEntity } from "@/lib/configApi";

// p0345: form field primitives for the studio drawer. RefSelect / MultiRefSelect
// are the load-bearing pieces — a reference is only ever CHOSEN from the catalog
// (a <select> or a checkbox set), never typed, so an unknown ref cannot be
// entered in the first place.

export function TextField({
  label,
  value,
  onChange,
  placeholder,
  mono,
  testId,
  disabled,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  mono?: boolean;
  testId?: string;
  disabled?: boolean;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="eyebrow-uppercase text-stone-400">{label}</span>
      <input
        type="text"
        data-testid={testId}
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        className={cn(
          "rounded-md border border-stone-300 bg-white px-3 py-1.5 dsh-body text-stone-800 outline-none focus:border-[var(--color-primary)] disabled:bg-stone-100 disabled:text-stone-400",
          mono && "font-mono dsh-mono",
        )}
      />
    </label>
  );
}

// A single-FK picker. Options are the catalog entries of the target kind — the
// empty option forces an explicit choice and keeps the value resolvable.
export function RefSelect({
  label,
  value,
  options,
  onChange,
  placeholder = "— pick —",
  testId,
}: {
  label: string;
  value: string;
  options: StudioEntity[];
  onChange: (v: string) => void;
  placeholder?: string;
  testId?: string;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="eyebrow-uppercase text-stone-400">{label}</span>
      <select
        data-testid={testId}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="rounded-md border border-stone-300 bg-white px-3 py-1.5 dsh-body font-mono text-stone-800 outline-none focus:border-[var(--color-primary)]"
      >
        <option value="">{placeholder}</option>
        {options.map((o) => (
          <option key={o.id} value={o.id}>
            {o.id}
          </option>
        ))}
      </select>
    </label>
  );
}

// A FK-set picker rendered as toggle chips — a chosen id is added/removed from
// the set. No free-text path exists.
export function MultiRefSelect({
  label,
  values,
  options,
  onChange,
  testId,
}: {
  label: string;
  values: string[];
  options: StudioEntity[];
  onChange: (v: string[]) => void;
  testId?: string;
}) {
  const toggle = (id: string) =>
    onChange(values.includes(id) ? values.filter((v) => v !== id) : [...values, id]);
  return (
    <div className="flex flex-col gap-1" data-testid={testId}>
      <span className="eyebrow-uppercase text-stone-400">{label}</span>
      <div className="flex flex-wrap gap-2">
        {options.length === 0 && (
          <span className="dsh-label text-stone-400">no entries in catalog</span>
        )}
        {options.map((o) => {
          const on = values.includes(o.id);
          return (
            <button
              key={o.id}
              type="button"
              data-testid={`${testId}-option-${o.id}`}
              data-selected={on ? "true" : "false"}
              aria-pressed={on}
              onClick={() => toggle(o.id)}
              className={cn(
                "select-none rounded-full border px-3 py-1 dsh-body font-mono transition",
                on
                  ? "border-[var(--color-primary)] bg-emerald-50 text-emerald-700"
                  : "border-stone-300 bg-white text-stone-500 hover:border-stone-400",
              )}
            >
              {o.id}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// A comma-separated free-list (pipelines, trigger-like plain values) — this is
// NOT a reference, so free text is correct here.
export function ListField({
  label,
  values,
  onChange,
  placeholder,
  testId,
}: {
  label: string;
  values: string[];
  onChange: (v: string[]) => void;
  placeholder?: string;
  testId?: string;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="eyebrow-uppercase text-stone-400">{label}</span>
      <input
        type="text"
        data-testid={testId}
        value={values.join(", ")}
        placeholder={placeholder}
        onChange={(e) =>
          onChange(
            e.target.value
              .split(",")
              .map((s) => s.trim())
              .filter(Boolean),
          )
        }
        className="rounded-md border border-stone-300 bg-white px-3 py-1.5 dsh-body text-stone-800 outline-none focus:border-[var(--color-primary)]"
      />
    </label>
  );
}
