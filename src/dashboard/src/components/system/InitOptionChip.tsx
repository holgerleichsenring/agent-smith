"use client";

import type { CSSProperties } from "react";

// 2026-10-04-2bf2: the init action's option toggles are one component. "Auto-accept PRs"
// (p0490) was drawn inline; "Refresh principles" joined it, and two chips copied from one
// another would drift apart the first time either was touched. Each option is independent —
// this component knows nothing about any other chip, so none can disable or warn another.

/** The .pick chip the repo picker established, as inline tokens so the one
 *  definition covers both hosts the init action renders in — the config card
 *  (.mock-config) and the system project panel (.mock-system). */
// Longhand throughout, never `border`/`font` shorthand: the checked state overrides
// borderColor and fontWeight, and React warns that removing a longhand beside a
// shorthand is how styling bugs start.
export const CHIP: CSSProperties = {
  fontSize: "12.5px",
  fontFamily: "var(--mono)",
  fontWeight: 400,
  padding: "6px 11px",
  borderRadius: "9px",
  borderWidth: "1px",
  borderStyle: "solid",
  borderColor: "var(--line)",
  background: "var(--panel)",
  color: "var(--ink-2)",
  cursor: "pointer",
  display: "inline-flex",
  alignItems: "center",
  gap: "7px",
};

const CHECKED: CSSProperties = {
  borderColor: "var(--accent)",
  background: "var(--accent-wash)",
  color: "var(--accent)",
  fontWeight: 600,
};

const DISABLED: CSSProperties = { opacity: 0.5, cursor: "not-allowed" };

export interface InitOptionChipProps {
  /** The native checkbox's test id; the drawn tick box is `boxTestId`. */
  testId: string;
  boxTestId: string;
  /** What ticking it does, shown on hover. */
  title: string;
  label: string;
  checked: boolean;
  disabled: boolean;
  onChange: (next: boolean) => void;
}

export function InitOptionChip({
  testId,
  boxTestId,
  title,
  label,
  checked,
  disabled,
  onChange,
}: InitOptionChipProps) {
  return (
    <label
      title={title}
      onClick={(e) => e.stopPropagation()}
      style={{ ...CHIP, ...(checked ? CHECKED : null), ...(disabled ? DISABLED : null) }}
    >
      <input
        type="checkbox"
        className="sr-only"
        data-testid={testId}
        checked={checked}
        disabled={disabled}
        onChange={(e) => onChange(e.target.checked)}
      />
      {/* The repo picker's .pk tick box, drawn from the same tokens. */}
      <span
        aria-hidden="true"
        data-testid={boxTestId}
        style={{
          width: 15,
          height: 15,
          borderRadius: 4,
          display: "grid",
          placeItems: "center",
          fontSize: 10,
          lineHeight: 1,
          borderWidth: "1.5px",
          borderStyle: "solid",
          borderColor: checked ? "var(--accent)" : "var(--line)",
          background: checked ? "var(--accent)" : "transparent",
          color: checked ? "var(--accent-ink)" : "transparent",
        }}
      >
        ✓
      </span>
      {label}
    </label>
  );
}
