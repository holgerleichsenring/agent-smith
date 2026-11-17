"use client";

import type { SettingValue, TicketsSetting } from "@/lib/configApi";
import { NumberField, keep, patcher } from "./formFields";

// 2026-11-17-f9b0d: the tickets settings singleton, read live by the filing, the dependents wake and the
// pull-request re-read. A value below 1 is refused by the server on save.

export function TicketsForm({ value, onChange }: { value: TicketsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const field = (label: string, key: keyof TicketsSetting, testId: string, help: string) => (
    <NumberField label={label} value={value[key]} onChange={(v) => set({ [key]: keep(v, value[key]) })}
      testId={testId} help={help} />
  );
  return (
    <>
      {field("Requires link deadline (seconds)", "requiresLinkDeadlineSeconds", "setting-tickets-linkdeadline",
        "how long one filing waits for its tracker to show the requires links")}
      {field("Dependents wake window (hours)", "dependentsWakeWindowHours", "setting-tickets-wakewindow",
        "landings whose waiting tickets the recovery re-wakes")}
      {field("Dependents wake cap", "dependentsWakeCap", "setting-tickets-wakecap", "the most waiting tickets one recovery pass reads")}
      {field("Re-read interval (seconds)", "prRereadIntervalSeconds", "setting-tickets-rereadinterval",
        "time between two pull-request re-read passes")}
      {field("Re-read staleness (seconds)", "prRereadStaleSeconds", "setting-tickets-rereadstale",
        "how long a pull-request record goes unchecked before the re-read names it")}
      {field("Re-read open cap", "prRereadOpenCap", "setting-tickets-rereadopen", "the most open or unread records one pass names")}
      {field("Re-read merged cap", "prRereadMergedCap", "setting-tickets-rereadmerged",
        "the most merged records without a landing one pass names")}
    </>
  );
}
