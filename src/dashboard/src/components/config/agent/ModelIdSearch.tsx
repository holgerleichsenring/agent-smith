"use client";

import { useId, useState } from "react";
import type { ListedModelPrice } from "@/lib/configApi";

// 2026-09-30-62bab: the model id as a search over the public price list. The id stays
// free text — a model the list does not know is still enterable, and then needs an
// override — but picking from the list is one click and carries its price and window.

const MAX_MATCHES = 8;

export function ModelIdSearch({
  value,
  list,
  onChange,
}: {
  value: string;
  list: ListedModelPrice[] | null;
  onChange: (model: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const listId = useId();
  const query = value.trim().toLowerCase();
  const matches =
    open && list && query.length >= 2
      ? list.filter((m) => m.id.toLowerCase().includes(query) && m.id.toLowerCase() !== query).slice(0, MAX_MATCHES)
      : [];

  return (
    <div className="field">
      <label htmlFor={`${listId}-input`}>
        model it runs
        <span className="req">required</span>
        <span className="help">search the price list; the deployment name is separate</span>
      </label>
      <input
        id={`${listId}-input`}
        type="text"
        className="mono"
        role="combobox"
        aria-expanded={matches.length > 0}
        aria-controls={listId}
        aria-autocomplete="list"
        autoComplete="off"
        data-testid="agent-model-id"
        value={value}
        placeholder="e.g. gpt-4.1"
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          setOpen(true);
          onChange(e.target.value);
        }}
        onKeyDown={(e) => {
          if (e.key === "Escape") setOpen(false);
        }}
      />
      {matches.length > 0 && (
        <div className="repo-rows" id={listId} role="listbox" aria-label="price list matches" data-testid="agent-model-id-matches">
          {matches.map((m) => (
            <button
              key={m.id}
              type="button"
              role="option"
              aria-selected={false}
              className="repo-row"
              data-testid={`agent-model-id-match-${m.id}`}
              onClick={() => {
                onChange(m.id);
                setOpen(false);
              }}
              style={{ background: "none", border: 0, borderBottom: "1px solid var(--line)", width: "100%", textAlign: "left", cursor: "pointer", minHeight: 36 }}
            >
              <span className="rr-name">{m.id}</span>
              <span className="rr-branch">
                ${m.inputPerMillion} / ${m.outputPerMillion}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
