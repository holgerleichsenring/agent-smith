"use client";

import type { BranchCleanupSetting, PreconditionsSetting, SettingValue } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { CheckField, NumberField, keep, patcher } from "./formFields";

// 2026-11-17-b34eb: the two server sweeps' settings — the stuck-on-precondition recheck (2026-11-16-f3fbc) and the
// branch cleanup (2026-11-17-713ec). The server refuses what its load rules refuse; these only edit.

export function PreconditionsForm({ value, onChange }: { value: PreconditionsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <NumberField label={t("config.preconditions.recheck.label")} value={value.recheckIntervalSeconds}
        onChange={(v) => set({ recheckIntervalSeconds: keep(v, value.recheckIntervalSeconds) })}
        testId="setting-preconditions-interval"
        help={t("config.preconditions.recheck.help")} />
      {/* 2026-11-22-747dc: the interrupted-run restart window, preconditions.interrupted_restart_window_minutes */}
      <NumberField label={t("config.preconditions.restartWindow.label")} value={value.interruptedRestartWindowMinutes}
        onChange={(v) => set({ interruptedRestartWindowMinutes: keep(v, value.interruptedRestartWindowMinutes) })}
        testId="setting-preconditions-restart-window"
        help={t("config.preconditions.restartWindow.help")} />
    </>
  );
}

export function BranchCleanupForm({ value, onChange }: { value: BranchCleanupSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <CheckField label="Run branch cleanup" value={value.enabled}
        onChange={(v) => set({ enabled: v })} testId="setting-branch-cleanup-enabled" />
      <NumberField label="Interval (seconds)" value={value.intervalSeconds}
        onChange={(v) => set({ intervalSeconds: keep(v, value.intervalSeconds) })}
        testId="setting-branch-cleanup-interval"
        help="seconds between two branch-cleanup runs of a project; each run spends host API calls" />
    </>
  );
}
