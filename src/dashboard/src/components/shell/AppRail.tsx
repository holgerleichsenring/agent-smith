"use client";

import { usePathname } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { AppRailItem } from "./AppRailItem";

// p0209a: persistent left app rail (248px) replacing the topbar nav. Brand +
// connection dot at the top, then three sections — Runs / System / Rollups.
// Navigation is ROUTE-based: every item is a real route and the active item
// derives from usePathname, so selection is URL-stable and refresh-/deep-link
// safe by construction (no separate selection state). Per-subsystem live/
// freshness is the job of p0209b's useSubsystemActivity hook; this slice
// renders subsystem items idle (honest — no fake freshness here).

interface RailItem {
  id: string;
  label: string;
  href: string;
}

const SUBSYSTEMS: RailItem[] = [
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

  const isActive = (href: string) =>
    href === "/" ? pathname === "/" : pathname === href;

  return (
    <nav
      data-testid="app-rail"
      className="flex h-screen flex-col gap-0.5 border-r border-stone-200 py-4"
    >
      <div className="flex items-center gap-2.5 px-5 pb-2 text-[16px] font-bold text-stone-900">
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
      {SUBSYSTEMS.map((s) => (
        <AppRailItem key={s.id} label={s.label} href={s.href} active={isActive(s.href)} />
      ))}

      <Section label="Rollups" />
      {ROLLUPS.map((r) => (
        <AppRailItem key={r.id} label={r.label} href={r.href} active={isActive(r.href)} />
      ))}
    </nav>
  );
}

function Section({ label }: { label: string }) {
  return (
    <div
      data-testid={`app-rail-section-${label}`}
      className="px-5 pb-1.5 pt-4 text-[11px] font-bold uppercase tracking-[0.09em] text-stone-400"
    >
      {label}
    </div>
  );
}
