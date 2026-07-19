"use client";

import { useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { StudioEntity } from "@/lib/configApi";

// p0345: form field primitives for the studio drawer. RefSelect / MultiRefSelect
// are the load-bearing pieces — a reference is only ever CHOSEN from the catalog
// (a <select> or a pick set), never typed, so an unknown ref cannot be entered.
// p0343c (pixel identity): every field emits the config-studio.html form DOM —
// .field label+input/select, and the FK set as .picks of .pick buttons with the
// .pk check square.
// p0345c adds the capabilities-driven vocabulary: SelectField (a plain string
// dropdown fed from the capabilities descriptor), NumberField/CheckField for
// the full agent surface, and DrawerSection (collapsible drawer sections).

export function TextField({
  label,
  value,
  onChange,
  placeholder,
  mono,
  testId,
  disabled,
  required,
  help,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  mono?: boolean;
  testId?: string;
  disabled?: boolean;
  required?: boolean;
  help?: string;
}) {
  return (
    <div className="field">
      <label>
        {label}
        {required && <span className="req">required</span>}
        {help && <span className="help">{help}</span>}
      </label>
      <input
        type="text"
        data-testid={testId}
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        className={mono ? "mono" : undefined}
      />
    </div>
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
    <div className="field">
      <label>{label}</label>
      <select data-testid={testId} value={value} onChange={(e) => onChange(e.target.value)} className="mono">
        <option value="">{placeholder}</option>
        {options.map((o) => (
          <option key={o.id} value={o.id}>
            {o.id}
          </option>
        ))}
      </select>
    </div>
  );
}

// A FK-set picker rendered as the mock's .picks toggle chips — a chosen id is
// added/removed from the set. No free-text path exists.
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
    <div className="field" data-testid={testId}>
      <label>
        {label} <span className="help">pick from the catalog</span>
      </label>
      <div className="picks">
        {options.length === 0 && <span className="help">no entries in catalog</span>}
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
              className={cn("pick", on && "on")}
            >
              <span className="pk">{on ? "✓" : ""}</span>
              {o.id}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// p0345c: a plain string dropdown fed from the capabilities descriptor (tracker
// type, connection type, agent provider, pipeline, resolution strategy). The
// current value stays selectable even if capabilities do not list it (a stale
// entity must remain editable, never silently rewritten).
export function SelectField({
  label,
  value,
  options,
  onChange,
  placeholder = "— pick —",
  testId,
  required,
  help,
}: {
  label: string;
  value: string;
  options: string[];
  onChange: (v: string) => void;
  placeholder?: string;
  testId?: string;
  required?: boolean;
  help?: string;
}) {
  const opts = value && !options.includes(value) ? [value, ...options] : options;
  return (
    <div className="field">
      <label>
        {label}
        {required && <span className="req">required</span>}
        {help && <span className="help">{help}</span>}
      </label>
      <select data-testid={testId} value={value} onChange={(e) => onChange(e.target.value)} className="mono">
        <option value="">{placeholder}</option>
        {opts.map((o) => (
          <option key={o} value={o}>
            {o}
          </option>
        ))}
      </select>
    </div>
  );
}

// p0345c: a numeric field for the agent surface — empty input honestly maps to
// `undefined` (the section field stays unset), never to a fake 0.
export function NumberField({
  label,
  value,
  onChange,
  placeholder,
  testId,
  help,
}: {
  label: string;
  value: number | undefined;
  onChange: (v: number | undefined) => void;
  placeholder?: string;
  testId?: string;
  help?: string;
}) {
  return (
    <div className="field">
      <label>
        {label}
        {help && <span className="help">{help}</span>}
      </label>
      <input
        type="number"
        data-testid={testId}
        value={value ?? ""}
        placeholder={placeholder}
        className="mono"
        onChange={(e) => {
          const raw = e.target.value.trim();
          onChange(raw === "" ? undefined : Number(raw));
        }}
      />
    </div>
  );
}

// p0345c: a boolean toggle rendered as the mock's .pick chip (on/off).
export function CheckField({
  label,
  value,
  onChange,
  testId,
}: {
  label: string;
  value: boolean;
  onChange: (v: boolean) => void;
  testId?: string;
}) {
  return (
    <div className="field">
      <label>{label}</label>
      <div className="picks">
        <button
          type="button"
          data-testid={testId}
          data-selected={value ? "true" : "false"}
          aria-pressed={value}
          onClick={() => onChange(!value)}
          className={cn("pick", value && "on")}
        >
          <span className="pk">{value ? "✓" : ""}</span>
          {value ? "enabled" : "disabled"}
        </button>
      </div>
    </div>
  );
}

// p0345c: one collapsible drawer section (the agent form's Provider & endpoint /
// Models / Pricing / Cache / Compaction / Retry). The header shows whether the
// section carries values; an empty optional section is simply not persisted.
export function DrawerSection({
  title,
  summary,
  defaultOpen,
  testId,
  children,
}: {
  title: string;
  summary?: string;
  defaultOpen?: boolean;
  testId?: string;
  children: ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen ?? false);
  return (
    <div className="dsec" data-testid={testId} data-open={open ? "true" : "false"}>
      <button type="button" className="dsec-h" onClick={() => setOpen((o) => !o)} data-testid={testId ? `${testId}-toggle` : undefined}>
        <span className="chev">{open ? "▾" : "▸"}</span>
        {title}
        {summary && <span className="dcount">{summary}</span>}
      </button>
      {open && <div className="dsec-b">{children}</div>}
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
    <div className="field">
      <label>{label}</label>
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
      />
    </div>
  );
}
