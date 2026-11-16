"use client";

import { usePathname } from "next/navigation";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, type SubsystemActivity, type SubsystemId } from "@/hooks/useSubsystemActivity";
import { SectionNav, SectionNavGroup } from "@/components/shell/SectionNav";
import { SectionNavItem } from "@/components/shell/SectionNavItem";

// p0209a: the running system's parts, each with its liveness. 2026-11-16-0440c: moved out of the
// app rail into the /system layout, beside its pages. A bare /system shows the tracker.

const SUBSYSTEM_ITEMS: ReadonlyArray<{ id: SubsystemId; label: string; href: string }> = [
  { id: "tracker", label: "Tracker · ticket polling", href: "/system/tracker" },
  { id: "webhooks", label: "Webhooks", href: "/system/webhooks" },
  { id: "chat", label: "Chat dispatchers", href: "/system/chat" },
  { id: "config", label: "Config file reads", href: "/system/config" },
  { id: "catalog", label: "Skill catalog & vocabulary", href: "/system/catalog" },
];

export function SystemSectionNav() {
  const pathname = usePathname() ?? "";
  // Liveness for EVERY subsystem, so it reads the full shared backlog.
  const activity = useSubsystemActivity(useSystemBacklog());
  const trackerLabel = trackerLabelFor(activity.tracker);

  return (
    <SectionNav label="System" testId="system-nav">
      <SectionNavGroup label="System" />
      {SUBSYSTEM_ITEMS.map((s) => (
        <SectionNavItem
          key={s.id}
          // 2026-08-27-1ed6: the Tracker entry names the tracker actually seen.
          label={s.id === "tracker" && trackerLabel ? trackerLabel : s.label}
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
function trackerLabelFor(tracker: SubsystemActivity): string | null {
  const newest = tracker.events[tracker.events.length - 1] as { tracker?: string } | undefined;
  return newest?.tracker ? `Tracker · ${newest.tracker}` : null;
}
