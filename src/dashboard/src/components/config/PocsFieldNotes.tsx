"use client";

import { useT } from "@/hooks/useT";

// 2026-11-24-f965: what a PoCs field is missing, at the field. "required" while pocs.enabled is on and the field is
// empty — before any save; and the server's own sentences after a refused save, placed by their key (pocsProblems).

export function PocsFieldNotes({ testId, missing = false, problems = [] }: {
  testId: string; missing?: boolean; problems?: string[];
}) {
  const t = useT();
  if (!missing && problems.length === 0) return null;
  return (
    <div data-testid={`${testId}-notes`} style={{ display: "flex", flexDirection: "column", gap: 2 }}>
      {missing && problems.length === 0 && (
        <span className="help" data-testid={`${testId}-required`} style={{ color: "var(--bad)" }}>
          {t("config.pocs.field.required")}
        </span>
      )}
      {problems.map((problem) => (
        <span key={problem} className="help" data-testid={`${testId}-problem`} style={{ color: "var(--bad)" }}>
          {problem}
        </span>
      ))}
    </div>
  );
}
