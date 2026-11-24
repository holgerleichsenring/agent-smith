"use client";

import type { SettingValue, TicketsSetting } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { NumberField, keep, patcher } from "./formFields";

// 2026-11-17-f9b0d: the tickets settings singleton, read live by the filing, the dependents wake and the
// pull-request re-read. A value below 1 is refused by the server on save.

export function TicketsForm({ value, onChange }: { value: TicketsSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  const field = (label: string, key: keyof TicketsSetting, testId: string, help: string) => (
    <NumberField label={label} value={value[key]} onChange={(v) => set({ [key]: keep(v, value[key]) })}
      testId={testId} help={help} />
  );
  return (
    <>
      {field(t("config.tickets.linkDeadline.label"), "requiresLinkDeadlineSeconds", "setting-tickets-linkdeadline",
        t("config.tickets.linkDeadline.help"))}
      {field(t("config.tickets.wakeWindow.label"), "dependentsWakeWindowHours", "setting-tickets-wakewindow",
        t("config.tickets.wakeWindow.help"))}
      {field(t("config.tickets.wakeCap.label"), "dependentsWakeCap", "setting-tickets-wakecap", t("config.tickets.wakeCap.help"))}
      {field(t("config.tickets.rereadInterval.label"), "prRereadIntervalSeconds", "setting-tickets-rereadinterval",
        t("config.tickets.rereadInterval.help"))}
      {field(t("config.tickets.rereadStale.label"), "prRereadStaleSeconds", "setting-tickets-rereadstale",
        t("config.tickets.rereadStale.help"))}
      {field(t("config.tickets.rereadOpen.label"), "prRereadOpenCap", "setting-tickets-rereadopen", t("config.tickets.rereadOpen.help"))}
      {field(t("config.tickets.rereadMerged.label"), "prRereadMergedCap", "setting-tickets-rereadmerged",
        t("config.tickets.rereadMerged.help"))}
    </>
  );
}
