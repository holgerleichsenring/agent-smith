"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import type { StudioEntity } from "@/lib/configApi";

// p0345: form field primitives for the studio drawer. RefSelect / MultiRefSelect
// are the load-bearing pieces — a reference is only ever CHOSEN from the catalog
// (a <select> or a pick set), never typed, so an unknown ref cannot be entered.
// p0343c (pixel identity): every field emits the config-studio.html form DOM —
// .field label+input/select, and the FK set as .picks of .pick buttons with the
// .pk check square.

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

// p0345b: connection-scoped repo refs ("conn/RepoName"). The CONNECTION half is
// a real FK picked from the catalog; the repo NAME half is discovered at run
// time inside that connection, so free text is honest there. Existing refs
// render as removable .pick chips.
export function ConnRefField({
  label,
  values,
  connections,
  onChange,
  testId,
}: {
  label: string;
  values: string[];
  connections: StudioEntity[];
  onChange: (v: string[]) => void;
  testId?: string;
}) {
  const [connection, setConnection] = useState("");
  const [repoName, setRepoName] = useState("");
  const candidate = connection && repoName.trim() ? `${connection}/${repoName.trim()}` : null;
  const add = () => {
    if (!candidate || values.includes(candidate)) return;
    onChange([...values, candidate]);
    setRepoName("");
  };
  return (
    <div className="field" data-testid={testId}>
      <label>{label}</label>
      <div className="picks">
        {values.length === 0 && <span className="help">no connection-scoped repos</span>}
        {values.map((ref) => (
          <span key={ref} data-testid={`${testId}-chip-${ref}`} className="pick on">
            {ref}
            <button
              type="button"
              aria-label={`Remove ${ref}`}
              data-testid={`${testId}-remove-${ref}`}
              onClick={() => onChange(values.filter((v) => v !== ref))}
              style={{ background: "none", border: 0, cursor: "pointer", color: "inherit", font: "inherit" }}
            >
              ×
            </button>
          </span>
        ))}
      </div>
      <div style={{ display: "flex", gap: 9, alignItems: "flex-end" }}>
        <div className="field" style={{ flex: 1 }}>
          <label>connection</label>
          <select
            data-testid={`${testId}-connection`}
            value={connection}
            onChange={(e) => setConnection(e.target.value)}
            className="mono"
          >
            <option value="">— pick —</option>
            {connections.map((c) => (
              <option key={c.id} value={c.id}>
                {c.id}
              </option>
            ))}
          </select>
        </div>
        <div className="field" style={{ flex: 1 }}>
          <label>repo name</label>
          <input
            type="text"
            data-testid={`${testId}-name`}
            value={repoName}
            placeholder="RepoName"
            className="mono"
            onChange={(e) => setRepoName(e.target.value)}
          />
        </div>
        <button
          type="button"
          className="pick"
          data-testid={`${testId}-add`}
          disabled={!candidate}
          onClick={add}
          style={!candidate ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
        >
          Add
        </button>
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
