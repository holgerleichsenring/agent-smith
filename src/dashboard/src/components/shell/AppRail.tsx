"use client";

import { usePathname } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, type SubsystemId } from "@/hooks/useSubsystemActivity";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { AppRailItem } from "./AppRailItem";

// p0209a: persistent left app rail (248px) replacing the topbar nav. Brand +
// connection dot at the top, then three sections — Runs / System / Rollups.
// Navigation is ROUTE-based: every item is a real route and the active item
// derives from usePathname, so selection is URL-stable and refresh-/deep-link
// safe by construction (no separate selection state).
// p0209b: subsystem dots/freshness now come from useSubsystemActivity (same
// derivation the detail pane uses), so the rail and the open subsystem agree.

interface RailItem {
  id: string;
  label: string;
  href: string;
}

const SUBSYSTEM_ITEMS: Array<RailItem & { id: SubsystemId }> = [
  { id: "tracker", label: "Tracker · ticket polling", href: "/system/tracker" },
  { id: "webhooks", label: "Webhooks", href: "/system/webhooks" },
  { id: "chat", label: "Chat dispatchers", href: "/system/chat" },
  { id: "config", label: "Config file reads", href: "/system/config" },
  { id: "catalog", label: "Skill catalog & vocabulary", href: "/system/catalog" },
];

const ROLLUPS: RailItem[] = [
  { id: "cost", label: "Cost", href: "/system/cost" },
  { id: "today", label: "Today's activity", href: "/system/today" },
];

export function AppRail() {
  const pathname = usePathname();
  const { connectionState } = useJobsHub();
  const connected = connectionState === HubConnectionState.Connected;
  // The rail shows liveness for EVERY subsystem, so it reads the full shared
  // backlog (not one subsystem's scope).
  const events = useSystemBacklog();
  const activity = useSubsystemActivity(events);

  const isActive = (href: string) =>
    href === "/" ? pathname === "/" : pathname === href;

  return (
    <nav
      data-testid="app-rail"
      className="flex h-screen flex-col gap-0.5 border-r border-stone-200 py-4"
    >
      <div className="flex items-center gap-2.5 px-5 pb-2 dsh-h3 font-bold text-stone-900">
        <span className="h-2.5 w-2.5 rounded-full bg-[var(--color-primary)]" aria-hidden />
        agent-smith
        <span
          data-testid="app-rail-connection"
          className={`ml-auto h-2 w-2 rounded-full ${connected ? "bg-emerald-500" : "bg-stone-400"}`}
          aria-label={connected ? "connected" : "disconnected"}
        />
      </div>

      <Section label="Runs" />
      <AppRailItem label="Runs" href="/" live={connected} active={isActive("/")} />

      <Section label="System" />
      {SUBSYSTEM_ITEMS.map((s) => (
        <AppRailItem
          key={s.id}
          label={s.label}
          href={s.href}
          live={activity[s.id].live}
          freshness={activity[s.id].freshness}
          active={isActive(s.href)}
        />
      ))}
      {/* p0292: Connections is an on-demand diagnostics page, not an event-stream
          subsystem — it has no live/freshness signal, so it renders plain. */}
      <AppRailItem
        label="Connections"
        href="/system/connections"
        active={isActive("/system/connections")}
      />

      <Section label="Rollups" />
      {ROLLUPS.map((r) => (
        <AppRailItem key={r.id} label={r.label} href={r.href} active={isActive(r.href)} />
      ))}
    </nav>
  );
}

function Section({ label }: { label: string }) {
  return (
    <SectionLabel testId={`app-rail-section-${label}`} className="px-5 pb-1.5 pt-4">
      {label}
    </SectionLabel>
  );
}
