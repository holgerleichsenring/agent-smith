"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, type SubsystemId, type SubsystemActivity } from "@/hooks/useSubsystemActivity";
import { fetchChanges, type ConfigEntityKind } from "@/lib/configApi";
import { useConfigCatalog } from "@/components/config/useConfigCatalog";
import { ENTITY_LABEL } from "@/components/config/entities";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { cn } from "@/lib/utils";
import { AppRailItem } from "./AppRailItem";

// p0209a: persistent left app rail (248px). p0343b (mock fidelity): the rail is
// CONTEXTUAL — a Runs|Configuration segmented toggle under the brand switches
// the surface, and the sections below follow it. Runs routes ("/", "/jobs/*",
// "/system/*") show MONITOR (live bucket counts) + SYSTEM + ROLLUPS + a
// tracker-status footer; /config routes show the CATALOG (entity counts) +
// HISTORY. Navigation stays ROUTE-based: active items derive from usePathname,
// so selection is URL-stable and refresh-/deep-link safe by construction.
// p0209b: subsystem dots/freshness come from useSubsystemActivity (the same
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
  // p0329: ratification outcomes → expectation-hit-rate / first-PR-acceptance.
  { id: "expectations", label: "Expectations", href: "/system/expectations" },
];

// The catalog entities the config rail lists, in the mock's order.
const CATALOG_KINDS: ConfigEntityKind[] = [
  "projects",
  "agents",
  "trackers",
  "repos",
  "connections",
  "mcp-servers",
  "secrets",
];

export function AppRail() {
  const pathname = usePathname();
  const { connectionState, overview } = useJobsHub();
  const connected = connectionState === HubConnectionState.Connected;
  // The rail shows liveness for EVERY subsystem, so it reads the full shared
  // backlog (not one subsystem's scope).
  const events = useSystemBacklog();
  const activity = useSubsystemActivity(events);
  // p0345b: LIVE monitor counts — the SAME merge + bucketing MissionControl
  // renders, so the rail can never disagree with the home sections it links to.
  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent) : []),
    [overview],
  );
  const buckets = useMemo(() => bucketRuns(runs), [runs]);

  const isActive = (href: string) =>
    href === "/" ? pathname === "/" : pathname === href;
  // p0345: the Configuration studio is a route subtree (/config/{section}) — any
  // path under it flips the rail into catalog mode.
  const configMode = pathname.startsWith("/config");

  return (
    <nav
      data-testid="app-rail"
      data-mode={configMode ? "config" : "runs"}
      className="flex h-screen flex-col gap-0.5 overflow-y-auto border-r border-stone-200 py-4"
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

      <RailToggle configMode={configMode} />

      {configMode ? (
        <ConfigRailSections pathname={pathname} />
      ) : (
        <>
          <Section label="Monitor" />
          {/* p0343b: Today = every run in the merged list, the section anchors
              below mirror MissionControl's buckets. Needs-you goes hot (amber)
              the moment a run waits on the operator. */}
          <AppRailItem
            label="Today"
            href="/"
            live={connected}
            active={isActive("/")}
            count={runs.length}
          />
          <AppRailItem
            label="Needs you"
            href="/#needs-you"
            active={false}
            indent
            count={buckets.needsYou.length}
            hot={buckets.needsYou.length > 0}
          />
          <AppRailItem
            label="Running"
            href="/#running"
            active={false}
            indent
            count={buckets.running.length}
          />
          <AppRailItem
            label="Queued"
            href="/#queued"
            active={false}
            indent
            count={buckets.queued.length}
          />
          <AppRailItem
            label="Finished"
            href="/#finished"
            active={false}
            indent
            count={buckets.finished.length}
          />

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
          {/* p0292: Connections is an on-demand diagnostics page, not an
              event-stream subsystem — no live/freshness signal, renders plain. */}
          <AppRailItem
            label="Connections"
            href="/system/connections"
            active={isActive("/system/connections")}
          />

          <Section label="Rollups" />
          {ROLLUPS.map((r) => (
            <AppRailItem key={r.id} label={r.label} href={r.href} active={isActive(r.href)} />
          ))}

          <RailFooter tracker={activity.tracker} webhooks={activity.webhooks} />
        </>
      )}
    </nav>
  );
}

// p0343b: the mock's Runs|Configuration segmented toggle — a two-button pill on
// the panel background; the active half reads as a white card.
function RailToggle({ configMode }: { configMode: boolean }) {
  return (
    <div className="px-4 pb-2 pt-1">
      <div
        data-testid="rail-toggle"
        className="grid grid-cols-2 gap-0.5 rounded-md border border-stone-200 bg-stone-100 p-0.5"
      >
        <ToggleHalf label="Runs" href="/" active={!configMode} testId="rail-toggle-runs" />
        <ToggleHalf
          label="Configuration"
          href="/config"
          active={configMode}
          testId="rail-toggle-config"
        />
      </div>
    </div>
  );
}

function ToggleHalf({
  label,
  href,
  active,
  testId,
}: {
  label: string;
  href: string;
  active: boolean;
  testId: string;
}) {
  return (
    <Link
      href={href}
      data-testid={testId}
      data-active={active ? "true" : "false"}
      aria-current={active ? "page" : undefined}
      className={cn(
        "select-none rounded px-2 py-1.5 text-center dsh-label font-semibold transition",
        active
          ? "bg-white text-stone-900 shadow-sm"
          : "text-stone-500 hover:text-stone-700",
      )}
    >
      {label}
    </Link>
  );
}

// p0343b: the config-mode rail — the entity CATALOG with live counts (the same
// list clients the studio itself loads) + HISTORY (Changes with its count).
// Replaces the studio's tab row per the ratified mock.
function ConfigRailSections({ pathname }: { pathname: string }) {
  const { catalog, loading } = useConfigCatalog();
  const [changesCount, setChangesCount] = useState<number | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchChanges(controller.signal)
      .then((changes) => setChangesCount(changes.length))
      .catch(() => setChangesCount(null));
    return () => controller.abort();
  }, []);

  return (
    <>
      <Section label="Catalog" />
      {CATALOG_KINDS.map((kind) => (
        <AppRailItem
          key={kind}
          label={ENTITY_LABEL[kind]}
          href={`/config/${kind}`}
          active={pathname === `/config/${kind}` || (kind === "agents" && pathname === "/config")}
          count={loading ? undefined : catalog[kind].length}
        />
      ))}
      <Section label="History" />
      <AppRailItem
        label="Changes"
        href="/config/changes"
        active={pathname === "/config/changes"}
        count={changesCount ?? undefined}
      />
    </>
  );
}

// p0343b: the runs-mode rail footer — inflow health at a glance. The tracker
// line names the tracker seen on its newest event and reuses the SAME freshness
// the SYSTEM items render; webhooks reduce to live/idle.
function RailFooter({
  tracker,
  webhooks,
}: {
  tracker: SubsystemActivity;
  webhooks: SubsystemActivity;
}) {
  const trackerName = newestTrackerName(tracker);
  return (
    <div
      data-testid="rail-footer"
      className="mt-auto space-y-1 border-t border-stone-200 px-5 pt-3"
    >
      <div data-testid="rail-footer-tracker" className="font-mono dsh-mono text-stone-400">
        {trackerName ?? "tracker"} · {tracker.freshness === "—" ? "no polls seen" : `polled ${tracker.freshness}`}
      </div>
      <div data-testid="rail-footer-webhooks" className="font-mono dsh-mono text-stone-400">
        webhooks · {webhooks.live ? "live" : "idle"}
      </div>
    </div>
  );
}

// Every tracker-subsystem event carries the tracker's name — read it off the
// newest one; null when no tracker event has been seen yet.
function newestTrackerName(tracker: SubsystemActivity): string | null {
  const newest = tracker.events[tracker.events.length - 1] as
    | { tracker?: string }
    | undefined;
  return newest?.tracker ?? null;
}

function Section({ label }: { label: string }) {
  return (
    <SectionLabel testId={`app-rail-section-${label}`} className="px-5 pb-1.5 pt-4">
      {label}
    </SectionLabel>
  );
}
