"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemBacklog } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, type SubsystemId, type SubsystemActivity } from "@/hooks/useSubsystemActivity";
import { type ConfigEntityKind } from "@/lib/configApi";
import { fetchPullRequests } from "@/lib/pullRequestsApi";
import { useConfigCatalogContext } from "@/components/config/ConfigCatalogProvider";
import { ENTITY_LABEL } from "@/components/config/entities";
import { SETTING_ICON, SETTING_KEYS, SETTING_LABEL } from "@/components/config/settings";
import { mergeNewestFirst } from "@/components/jobs/RunsList";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import {
  bucketHref,
  useRunBucketFilter,
  type RunBucketFilter,
} from "@/lib/RunBucketFilter";
import { cn } from "@/lib/utils";
import { AppRailItem } from "./AppRailItem";
import { isOpenPullRequest } from "@/lib/prStatus";

// p0209a: persistent left app rail. p0343c (pixel identity): the rail emits the
// ratified mocks' .side DOM verbatim — .brand (logo block + name), .nav-h section
// headings and .nav items with .ni icons and .nc counts. Runs routes show MONITOR
// (live bucket counts, hot needs-you) + SYSTEM + ROLLUPS styled consistently;
// /config routes show the CATALOG (mock icons + live counts), SETTINGS, ACCESS and
// THIS INSTALLATION.
// 2026-08-27-1ed6: the rail shows the RUNNING SYSTEM and nothing that is a setting.
// The Runs|Configuration toggle left with the header's gear (two entrances to one
// place), the identity block and release line moved into the header, and the tracker
// footer went — its one unique fact, the observed tracker's NAME, rides the Tracker
// entry's label instead.
// Navigation stays URL-based: destinations derive from usePathname and the
// monitor buckets from the shared ?bucket= filter, so selection is URL-stable
// and refresh-/deep-link safe by construction.
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
  // p0458: the monitor items select a bucket instead of scrolling to one.
  const { filter, select } = useRunBucketFilter();
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
  // p0347: the live open-PR count for the Monitor rail item. Fetched from the
  // same GET /api/pull-requests the page renders, so the rail count can never
  // disagree with the page's "Total open" metric. Null until the first fetch
  // lands — the item then renders without a count rather than a fake 0.
  const openPrCount = useOpenPrCount();

  const isActive = (href: string) =>
    href === "/" ? pathname === "/" : pathname === href;
  // A bucket is the current view only on the home screen — from anywhere else
  // no monitor item may claim to be what is on screen.
  const isChosen = (bucket: RunBucketFilter) => pathname === "/" && filter === bucket;
  const chooseBucket = (bucket: RunBucketFilter) => (event: React.MouseEvent) => {
    // Modified clicks stay browser business — the href is a real destination.
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    select(bucket);
  };
  // p0345: the Configuration studio is a route subtree (/config/{section}) — any
  // path under it flips the rail into catalog mode. 2026-08-27-1ed6: the installation
  // read-out and the connection check moved INTO that subtree, so the prefix is again
  // the whole answer and no list of excepted routes is needed.
  const configMode = pathname.startsWith("/config");
  // The tracker actually seen, on the entry that leads to it — a constant label cannot
  // say which of several configured trackers is polling.
  const trackerLabel = trackerLabelFor(activity.tracker);

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

      {configMode ? (
        <ConfigRailSections pathname={pathname} />
      ) : (
        <>
          <Section label="Monitor" />
          {/* p0348: "All runs" = every run in the merged active+recent list (no
              date filter — the old "Today" label was misleading).
              p0458: each item below SHOWS its bucket rather than scrolling to it —
              a label carrying a live count is read as a filter, so it is one.
              "All runs" clears the filter. Needs-you goes hot (amber) the moment
              a run waits on the operator. */}
          <AppRailItem
            label="All runs"
            href={bucketHref("all")}
            icon="◉"
            active={isChosen("all")}
            count={runs.length}
            onClick={chooseBucket("all")}
          />
          <AppRailItem
            label="Needs you"
            href={bucketHref("needs-you")}
            icon="?"
            active={isChosen("needs-you")}
            count={buckets.needsYou.length}
            hot={buckets.needsYou.length > 0}
            onClick={chooseBucket("needs-you")}
          />
          <AppRailItem
            label="Running"
            href={bucketHref("running")}
            icon="▶"
            active={isChosen("running")}
            count={buckets.running.length}
            onClick={chooseBucket("running")}
          />
          <AppRailItem
            label="Queued"
            href={bucketHref("queued")}
            icon="≡"
            active={isChosen("queued")}
            count={buckets.queued.length}
            onClick={chooseBucket("queued")}
          />
          <AppRailItem
            label="Finished"
            href={bucketHref("finished")}
            icon="✓"
            active={isChosen("finished")}
            count={buckets.finished.length}
            onClick={chooseBucket("finished")}
          />
          {/* p0347: agent-smith's OUTPUT — the PRs it opened — as its own
              monitor destination, with a live open-PR count. */}
          <AppRailItem
            label="Pull requests"
            href="/pull-requests"
            icon="↗"
            active={isActive("/pull-requests")}
            count={openPrCount ?? undefined}
          />

          <Section label="System" style={{ marginTop: 10 }} />
          {SUBSYSTEM_ITEMS.map((s) => (
            <AppRailItem
              key={s.id}
              /* 2026-08-27-1ed6: the Tracker entry names the tracker actually seen. */
              label={s.id === "tracker" && trackerLabel ? trackerLabel : s.label}
              href={s.href}
              live={activity[s.id].live}
              freshness={activity[s.id].freshness}
              active={isActive(s.href)}
            />
          ))}

          <Section label="Rollups" style={{ marginTop: 10 }} />
          {ROLLUPS.map((r) => (
            <AppRailItem key={r.id} label={r.label} href={r.href} icon={r.icon} active={isActive(r.href)} />
          ))}
        </>
      )}
    </nav>
  );
}

// p0343b: the config-mode rail — the entity CATALOG with live counts (the same
// list clients the studio itself loads) + HISTORY (Changes with its count).
function ConfigRailSections({ pathname }: { pathname: string }) {
  // p0353: the SHARED catalog + changes count — the same instance the studio
  // pane edits, so an import/save/revert's reload() refreshes these badges too.
  const { catalog, loading, changesCount } = useConfigCatalogContext();

  return (
    <>
      {/* 2026-08-27-1ed6: the way back. The rail no longer carries a two-way toggle —
          configuration is entered by the header's gear and left by this. */}
      <Link href="/" className="nav" data-testid="rail-back-to-runs">
        <span className="ni">←</span>Runs
      </Link>
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
      {/* p0353: the global SETTINGS singletons — one entry per settings doc, each a
          typed form. Singletons carry no count (there is exactly one of each). */}
      <Section label="Settings" style={{ marginTop: 10 }} />
      {SETTING_KEYS.map((key) => (
        <AppRailItem
          key={key}
          label={SETTING_LABEL[key]}
          href={`/config/settings/${key}`}
          icon={SETTING_ICON[key]}
          active={pathname === `/config/settings/${key}`}
        />
      ))}
      {/* 2026-08-26-7a51: who may do what. Its own section rather than a settings entry,
          because it needs access.read/access.write and not config.read/config.write. */}
      <Section label="Access" style={{ marginTop: 10 }} />
      <AppRailItem
        label="Permissions"
        href="/config/access"
        icon="◈"
        active={pathname === "/config/access"}
      />
      {/* 2026-08-27-1ed6: what THIS installation is — what it runs, whether its
          dependencies answer, and what has been changed on it. The first two arrived
          from /system, which is where the running system is watched; an installation is
          not a subsystem of itself. */}
      <Section label="This installation" style={{ marginTop: 10 }} />
      <AppRailItem
        label="Installation"
        href="/config/installation"
        icon="⬡"
        active={pathname === "/config/installation"}
      />
      <AppRailItem
        label="Connection check"
        href="/config/connection-check"
        icon="◳"
        active={pathname === "/config/connection-check"}
      />
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

// Every tracker-subsystem event carries the tracker's name — read it off the newest one.
// 2026-08-27-1ed6: this is the one fact the removed footer carried alone, so it rides the
// Tracker entry's label; otherwise which tracker is configured stops being visible
// anywhere in the shell. Null until an event names one — the entry keeps its plain name
// rather than claiming a tracker nobody has seen.
function trackerLabelFor(tracker: SubsystemActivity): string | null {
  const newest = tracker.events[tracker.events.length - 1] as { tracker?: string } | undefined;
  return newest?.tracker ? `Tracker · ${newest.tracker}` : null;
}

// p0347: the live count of OPENED pull requests for the Monitor rail item.
// Null until the first fetch lands (the item renders without a count then, never
// a fake 0). A failed fetch leaves it null — the rail stays honest.
function useOpenPrCount(): number | null {
  const [count, setCount] = useState<number | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetchPullRequests(controller.signal)
      .then((prs) => setCount(prs.filter((p) => isOpenPullRequest(p.status)).length))
      .catch(() => {
        /* honest: no count rather than a fabricated 0 */
      });
    return () => controller.abort();
  }, []);
  return count;
}

function Section({ label, style }: { label: string; style?: React.CSSProperties }) {
  return (
    <div className="nav-h" data-testid={`app-rail-section-${label}`} style={style}>
      {label}
    </div>
  );
}
