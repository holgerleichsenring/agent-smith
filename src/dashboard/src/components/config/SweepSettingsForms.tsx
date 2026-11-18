"use client";

import type { BranchCleanupSetting, PreconditionsSetting, SettingValue } from "@/lib/configApi";
import { CheckField, NumberField, keep, patcher } from "./formFields";

// 2026-11-17-b34eb: the two server sweeps' settings — the stuck-on-precondition recheck (2026-11-16-f3fbc) and the
// branch cleanup (2026-11-17-713ec). The server refuses what its load rules refuse; these only edit.

export function PreconditionsForm({ value, onChange }: { value: PreconditionsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <NumberField label="Recheck interval (seconds)" value={value.recheckIntervalSeconds}
      onChange={(v) => set({ recheckIntervalSeconds: keep(v, value.recheckIntervalSeconds) })}
      testId="setting-preconditions-interval"
      help="seconds between two passes over the tickets stuck on a precondition; at least 60" />
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
