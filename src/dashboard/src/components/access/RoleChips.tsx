"use client";

// 2026-08-26-7a51: the chips that say WHERE a role came from. One colour for a role the
// directory sends and cannot be taken back here, another for one granted here and
// withdrawn with a click — which is the whole reason the two are told apart at all.

export function RoleChips({
  testId,
  directory,
  granted,
  offered,
  onGrant,
  onWithdraw,
  grantLabel,
}: {
  testId: string;
  directory: { role: string; via: string }[];
  granted: string[];
  offered: string[];
  onGrant: (role: string) => void;
  onWithdraw: (role: string) => void;
  grantLabel: string;
}) {
  const open = offered.filter(
    (role) =>
      !granted.some((held) => held.toLowerCase() === role.toLowerCase())
      && !directory.some((origin) => origin.role.toLowerCase() === role.toLowerCase()),
  );
  return (
    <div className="roles" data-testid={testId}>
      {directory.map((origin) => (
        <span
          key={`${origin.via}:${origin.role}`}
          className="chip chip-directory"
          data-testid={`${testId}-directory-${origin.role}`}
        >
          <span className="src">{origin.via}</span>
          {origin.role}
        </span>
      ))}
      {granted.map((role) => (
        <span key={role} className="chip chip-here" data-testid={`${testId}-granted-${role}`}>
          <span className="src">granted</span>
          {role}
          <button
            type="button"
            aria-label={`Withdraw ${role}`}
            data-testid={`${testId}-withdraw-${role}`}
            onClick={() => onWithdraw(role)}
          >
            ×
          </button>
        </span>
      ))}
      {directory.length === 0 && granted.length === 0 && <span className="chip-none">no role</span>}
      {open.length > 0 && (
        <select
          className="grant"
          aria-label={grantLabel}
          data-testid={`${testId}-grant`}
          value=""
          onChange={(e) => e.target.value !== "" && onGrant(e.target.value)}
        >
          <option value="">+ grant</option>
          {open.map((role) => (
            <option key={role} value={role}>
              {role}
            </option>
          ))}
        </select>
      )}
    </div>
  );
}
