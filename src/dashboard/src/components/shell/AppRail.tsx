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
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { cn } from "@/lib/utils";
import { AppRailItem } from "./AppRailItem";

// p0209a: persistent left app rail. p0343c (pixel identity): the rail emits the
// ratified mocks' .side DOM verbatim — .brand (logo block + name), the
// Runs|Configuration .tabs pill, .nav-h section headings, .nav items with .ni
// icons and .nc counts, and the .tracker-foot footer. Runs routes show MONITOR
// (live bucket counts, hot needs-you) + SYSTEM + ROLLUPS styled consistently;
// /config routes show the CATALOG (mock icons + live counts) + HISTORY.
// Navigation stays ROUTE-based: active items derive from usePathname, so
// selection is URL-stable and refresh-/deep-link safe by construction.
// PROJECTS section: deliberately omitted — RunSnapshot carries no project
// field, so a per-project rail count would be fabricated.

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

const ROLLUPS: Array<RailItem & { icon: string }> = [
  { id: "cost", label: "Cost", href: "/system/cost", icon: "◍" },
  { id: "today", label: "Today's activity", href: "/system/today", icon: "◔" },
  // p0329: ratification outcomes → expectation-hit-rate / first-PR-acceptance.
  { id: "expectations", label: "Expectations", href: "/system/expectations", icon: "✓" },
];

// The catalog entities the config rail lists, with the mock's icons.
const CATALOG_KINDS: Array<{ kind: ConfigEntityKind; icon: string }> = [
  { kind: "projects", icon: "◈" },
  { kind: "agents", icon: "✦" },
  { kind: "trackers", icon: "◱" },
  { kind: "repos", icon: "⎇" },
  { kind: "connections", icon: "◳" },
  { kind: "mcp-servers", icon: "⇄" },
  { kind: "secrets", icon: "◍" },
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
      className="mock-shell side overflow-y-auto"
    >
      <div className="brand">
        <div className="logo">a</div>
        <div className="bn">agent-smith</div>
        <span
          data-testid="app-rail-connection"
          className={cn(
            "ml-auto inline-block h-2 w-2 rounded-full",
            connected ? "bg-[var(--ok)]" : "bg-[var(--idle)]",
          )}
          aria-label={connected ? "connected" : "disconnected"}
        />
      </div>

      <div className="tabs" data-testid="rail-toggle">
        <ToggleHalf label="Runs" href="/" active={!configMode} testId="rail-toggle-runs" />
        <ToggleHalf
          label="Configuration"
          href="/config"
          active={configMode}
          testId="rail-toggle-config"
        />
      </div>

      {configMode ? (
        <ConfigRailSections pathname={pathname} />
      ) : (
        <>
          <Section label="Monitor" />
          {/* p0343b: Today = every run in the merged list, the section anchors
              below mirror MissionControl's buckets. Needs-you goes hot (amber)
              the moment a run waits on the operator. */}
          <AppRailItem label="Today" href="/" icon="◉" active={isActive("/")} count={runs.length} />
          <AppRailItem
            label="Needs you"
            href="/#needs-you"
            icon="?"
            active={false}
            count={buckets.needsYou.length}
            hot={buckets.needsYou.length > 0}
          />
          <AppRailItem
            label="Running"
            href="/#running"
            icon="▶"
            active={false}
            count={buckets.running.length}
          />
          <AppRailItem
            label="Queued"
            href="/#queued"
            icon="≡"
            active={false}
            count={buckets.queued.length}
          />
          <AppRailItem
            label="Finished"
            href="/#finished"
            icon="✓"
            active={false}
            count={buckets.finished.length}
          />

          <Section label="System" style={{ marginTop: 10 }} />
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
            icon="◳"
            active={isActive("/system/connections")}
          />

          <Section label="Rollups" style={{ marginTop: 10 }} />
          {ROLLUPS.map((r) => (
            <AppRailItem key={r.id} label={r.label} href={r.href} icon={r.icon} active={isActive(r.href)} />
          ))}

          <RailFooter tracker={activity.tracker} webhooks={activity.webhooks} />
        </>
      )}
    </nav>
  );
}

// p0343b: the mock's Runs|Configuration segmented toggle — the .tabs pill.
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
      className={active ? "on" : undefined}
    >
      {label}
    </Link>
  );
}

// p0343b: the config-mode rail — the entity CATALOG with live counts (the same
// list clients the studio itself loads) + HISTORY (Changes with its count).
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
      {CATALOG_KINDS.map(({ kind, icon }) => (
        <AppRailItem
          key={kind}
          label={ENTITY_LABEL[kind]}
          href={`/config/${kind}`}
          icon={icon}
          active={pathname === `/config/${kind}` || (kind === "agents" && pathname === "/config")}
          count={loading ? undefined : catalog[kind].length}
        />
      ))}
      <Section label="History" style={{ marginTop: 10 }} />
      <AppRailItem
        label="Changes"
        href="/config/changes"
        icon="◔"
        active={pathname === "/config/changes"}
        count={changesCount ?? undefined}
      />
    </>
  );
}

// p0343b: the runs-mode rail footer — the mock's .tracker-foot. The tracker
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
    <div className="tracker-foot" data-testid="rail-footer">
      <div className="tf" data-testid="rail-footer-tracker">
        <span className={cn("td", !tracker.live && "idle")} />
        <span>
          <b style={{ color: "var(--ink-2)" }}>{trackerName ?? "tracker"}</b>
          {" · "}
          {tracker.freshness === "—" ? "no polls seen" : `polled ${tracker.freshness}`}
        </span>
      </div>
      <div className="tf" data-testid="rail-footer-webhooks">
        <span className={cn("td", !webhooks.live && "idle")} />
        <span>webhooks · {webhooks.live ? "live" : "idle"}</span>
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

function Section({ label, style }: { label: string; style?: React.CSSProperties }) {
  return (
    <div className="nav-h" data-testid={`app-rail-section-${label}`} style={style}>
      {label}
    </div>
  );
}
