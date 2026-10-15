"use client";

import { useState } from "react";

// 2026-10-15-4a4df: the members of a project binding — chips withdrawn with a click, a select
// offering the values this installation knows, and a typed value for one it does not, the way
// the Groups and People panes add theirs.

export function MemberChips({
  testId,
  label,
  members,
  offered,
  placeholder,
  onAdd,
  onRemove,
}: {
  testId: string;
  label: string;
  members: string[];
  offered: string[];
  placeholder: string;
  onAdd: (value: string) => void;
  onRemove: (value: string) => void;
}) {
  const [typed, setTyped] = useState("");
  const open = offered.filter((value) => !members.includes(value));

  function add() {
    const value = typed.trim();
    if (value === "" || members.includes(value)) return;
    setTyped("");
    onAdd(value);
  }

  return (
    <div className="roles members" data-testid={testId}>
      <span className="member-label">{label}</span>
      {members.map((member) => (
        <span key={member} className="chip chip-member" data-testid={`${testId}-chip-${member}`}>
          {member}
          <button
            type="button"
            aria-label={`Remove ${member}`}
            data-testid={`${testId}-remove-${member}`}
            onClick={() => onRemove(member)}
          >
            ×
          </button>
        </span>
      ))}
      {open.length > 0 && (
        <select
          className="grant"
          aria-label={`Add to ${label}`}
          data-testid={`${testId}-pick`}
          value=""
          onChange={(e) => e.target.value !== "" && onAdd(e.target.value)}
        >
          <option value="">+ pick</option>
          {open.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      )}
      <input
        className="member-field"
        type="text"
        aria-label={`Type a value for ${label}`}
        placeholder={placeholder}
        data-testid={`${testId}-value`}
        value={typed}
        onChange={(e) => setTyped(e.target.value)}
      />
      <button type="button" className="member-add" data-testid={`${testId}-add`} onClick={add}>
        Add
      </button>
    </div>
  );
}
