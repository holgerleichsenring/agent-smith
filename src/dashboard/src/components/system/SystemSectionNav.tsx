"use client";

import { usePathname } from "next/navigation";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, type SubsystemActivity, type SubsystemId } from "@/hooks/useSubsystemActivity";
import { SectionNav, SectionNavGroup } from "@/components/shell/SectionNav";
import { SectionNavItem } from "@/components/shell/SectionNavItem";
import { useAdminHoldCount } from "@/hooks/useAdminHoldCount";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";

// p0209a: the running system's parts, each with its liveness. 2026-11-16-0440c: moved out of the
// app rail into the /system layout, beside its pages. A bare /system shows the tracker.

const SUBSYSTEM_ITEMS: ReadonlyArray<{ id: SubsystemId; label: TextKey; href: string }> = [
  { id: "tracker", label: "system.nav.tracker", href: "/system/tracker" },
  { id: "webhooks", label: "system.nav.webhooks", href: "/system/webhooks" },
  { id: "chat", label: "system.nav.chat", href: "/system/chat" },
  { id: "config", label: "system.nav.config", href: "/system/config" },
  { id: "catalog", label: "system.nav.catalog", href: "/system/catalog" },
];

export const NEEDS_ATTENTION_PATH = "/system/attention";

export function SystemSectionNav() {
  const pathname = usePathname() ?? "";
  // Liveness for EVERY subsystem, so it reads the full shared backlog.
  const activity = useSubsystemActivity(useSystemBacklog());
  const t = useT();
  const trackerLabel = trackerLabelFor(activity.tracker, t);
  // 2026-11-23-31dci: the admin's page of every admin hold, with its count; a viewer known not to be an admin has none.
  const admin = useHoldsAdmin();
  const adminHolds = useAdminHoldCount();

  return (
    <SectionNav label={t("system.nav.label")} testId="system-nav">
      <SectionNavGroup label={t("system.nav.label")} />
      {admin && (
        <SectionNavItem
          label={t("attention.nav")}
          href={NEEDS_ATTENTION_PATH}
          icon="⚠"
          count={adminHolds}
          hot={(adminHolds ?? 0) > 0}
          active={pathname === NEEDS_ATTENTION_PATH}
        />
      )}
      {SUBSYSTEM_ITEMS.map((s) => (
        <SectionNavItem
          key={s.id}
          // 2026-08-27-1ed6: the Tracker entry names the tracker actually seen.
          label={s.id === "tracker" && trackerLabel ? trackerLabel : t(s.label)}
          href={s.href}
          live={activity[s.id].live}
          freshness={activity[s.id].freshness}
          active={pathname === s.href || (s.id === "tracker" && pathname === "/system")}
        />
      ))}
    </SectionNav>
  );
}

// Every tracker-subsystem event carries the tracker's name; read it off the newest one. Null
// until an event names one, so the entry never claims a tracker nobody has seen.
function trackerLabelFor(tracker: SubsystemActivity, t: T): string | null {
  const newest = tracker.events[tracker.events.length - 1] as { tracker?: string } | undefined;
  return newest?.tracker ? t("system.nav.trackerNamed", { tracker: newest.tracker }) : null;
}
