"use client";

import { useT } from "@/hooks/useT";
import { cn } from "@/lib/utils";

// 2026-11-24-35ff: one toggle above the runs list. Its label names how many system runs there are and, while they are
// hidden, how many of them failed, so a hidden failure still shows.

export function SystemRunsToggle({ show, system, systemFailed, onChange }: {
  show: boolean;
  system: number;
  systemFailed: number;
  onChange: (show: boolean) => void;
}) {
  const t = useT();
  const label = system === 0
    ? t("runs.system.toggle")
    : systemFailed > 0
      ? t("runs.system.toggleFailed", { count: system, failed: systemFailed })
      : t("runs.system.toggleCount", { count: system });
  const alarm = !show && systemFailed > 0;
  return (
    <label
      className={cn(
        "mb-4 inline-flex cursor-pointer items-center gap-2 text-xs",
        alarm ? "font-medium text-rose-700" : "text-stone-600",
      )}
      title={t("runs.system.title")}
      data-testid="system-runs-toggle"
      data-failed={alarm ? "true" : undefined}
    >
      <input
        type="checkbox"
        checked={show}
        onChange={(event) => onChange(event.target.checked)}
        data-testid="system-runs-checkbox"
      />
      <span>{label}</span>
    </label>
  );
}
