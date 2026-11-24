"use client";

import { useMemo } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  CalendarDays, ChartColumn, CirclePlay, Clock, GitFork, GitPullRequest, Inbox, LayoutGrid, MessagesSquare, PanelLeft, Plus, Rocket, Ticket, Terminal,
} from "lucide-react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useOpenPrCount } from "@/hooks/useOpenPrCount";
import { useSeriesRunCount } from "@/hooks/useSeriesRunCount";
import { useT } from "@/hooks/useT";
import { useRecentPlaces } from "@/hooks/useRecentPlaces";
import { useSidebarCollapsed } from "@/hooks/useSidebarCollapsed";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { totalTickets } from "@/components/tickets/ticketSections";
import { mergeNewestFirst } from "@/lib/runs/mergeNewestFirst";
import { bucketHref, RUNS_PATH, useRunBucketFilter } from "@/lib/RunBucketFilter";
import { TICKETS_PATH, ticketBucketHref, useTicketBucketFilter, type TicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { hasSectionBar } from "@/lib/sectionBarPage";
import { SERIES_PATH } from "@/lib/seriesRunAddress";
import { cn } from "@/lib/utils";
import { yourMoveCount } from "@/components/tickets/holds/holdWords";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";
import { useKnownIdentity } from "@/hooks/useKnownIdentity";
import { usePocsFeature } from "@/hooks/usePocsFeature";
import { sidebarItems, type SidebarItemId } from "./sidebarItems";
import { ProfileMenu } from "./ProfileMenu";
import { SidebarGroup, SidebarItem } from "./SidebarItem";

// 2026-11-16-0440c: the approved flat sidebar. New work on top, then the work itself (Your move,
// Tickets, Runs, Pull requests), the Insight pages and the pages this browser opened last. A
// list's states are pills on the list, not items here; settings sit behind the profile at the
// foot. Selection is read from the URL, so a reload or a deep link marks the same item.
//
// Your move is the tickets' Needs you bucket: nothing else on the server says what waits on a
// person across tickets and runs. Its count is that bucket's, so the item and the page it opens
// cannot disagree.
//
// There is no Projects group: no list page filters by project, so a project item would have
// nowhere true to go.

const isModified = (event: React.MouseEvent) => event.metaKey || event.ctrlKey || event.shiftKey || event.altKey;

export function AppSidebar() {
  const pathname = usePathname() ?? "/";
  const [chosen, toggle] = useSidebarCollapsed();
  // 2026-11-17-8d1ca: a ticket page has its own section bar beside this one, so the sidebar shows icons only there.
  // 2026-11-17-d762a: and so does Work it out, whose conversations are its bar.
  const ownBar = hasSectionBar(pathname);
  const collapsed = chosen || ownBar;
  const tickets = useTicketList().list?.counts;
  const yourMoves = yourMoveCount(tickets, useHoldsAdmin()); // 2026-11-23-31dcg: an admin's Your move adds the admin holds
  const ticketFilter = useTicketBucketFilter();
  const runFilter = useRunBucketFilter();
  const { overview } = useJobsHub();
  const openPrs = useOpenPrCount();
  const runningSeries = useSeriesRunCount(pathname); // 2026-11-23-31dcc
  const t = useT();
  const recent = useRecentPlaces(pathname);
  // 2026-11-23-933da: what this caller sees — sidebarItems decides, from the known identity and GET /api/pocs.
  const visible = new Set<SidebarItemId>(sidebarItems(useKnownIdentity(), usePocsFeature().status));
  const show = (item: SidebarItemId) => visible.has(item);

  // The same merge and bucketing the runs list renders, so the count matches it.
  const inFlight = useMemo(() => {
    if (!overview) return undefined;
    const buckets = bucketRuns(mergeNewestFirst(overview.active, overview.recent));
    return buckets.needsYou.length + buckets.running.length + buckets.queued.length;
  }, [overview]);

  const onTickets = pathname === TICKETS_PATH;
  const yourMoveActive = onTickets && ticketFilter.filter === "needs-you";
  const under = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  const chooseTickets = (bucket: TicketBucketFilter) => (event: React.MouseEvent) => {
    if (isModified(event)) return;
    event.preventDefault();
    ticketFilter.select(bucket);
  };
  const chooseRuns = (event: React.MouseEvent) => {
    if (isModified(event)) return;
    event.preventDefault();
    runFilter.select("all");
  };

  return (
    <nav aria-label={t("shell.nav.main")} data-testid="app-sidebar" data-collapsed={collapsed ? "true" : "false"} className={cn("app-sidebar", collapsed && "collapsed")}>
      <div className="sb-brand">
        {/* 2026-11-18-73ca: the brand is the way back to home. */}
        <Link href="/" className="sb-brand-link" aria-label={t("shell.nav.home")} data-testid="sidebar-home">
          <Terminal size={20} strokeWidth={2} aria-hidden="true" />
          {!collapsed && <span className="sb-brand-name">Agent Smith</span>}
        </Link>
        {!ownBar && <button
          type="button"
          className="sb-toggle"
          onClick={toggle}
          aria-label={collapsed ? t("shell.nav.expand") : t("shell.nav.collapse")}
          title={collapsed ? t("shell.nav.expand") : t("shell.nav.collapse")}
          data-testid="sidebar-toggle"
        >
          <PanelLeft size={16} strokeWidth={1.8} aria-hidden="true" />
        </button>}
      </div>

      {/* 2026-11-18-73ca: + starts new work on home's composer; the conversations are Work it out, below. */}
      {show("new-work") && <Link
        href="/"
        className={cn("sb-new", pathname === "/" && "on")}
        title={t("shell.nav.newWork")}
        aria-label={collapsed ? t("shell.nav.newWork") : undefined}
        aria-current={pathname === "/" ? "page" : undefined}
        data-testid="sidebar-new-work"
      >
        <Plus size={16} strokeWidth={2.2} aria-hidden="true" />
        {!collapsed && <span>{t("shell.nav.newWork")}</span>}
      </Link>}

      {show("work-it-out") && <SidebarItem
        label={t("shell.nav.workItOut")}
        href="/spec-dialog"
        icon={MessagesSquare}
        active={under("/spec-dialog")}
        collapsed={collapsed}
        testId="sidebar-work-it-out"
      />}
      {/* 2026-11-23-31dcc: every series a conversation filed, directly below the conversations that file them. */}
      {show("series") && <SidebarItem
        label={t("series.nav")}
        href={SERIES_PATH}
        icon={GitFork}
        active={under(SERIES_PATH)}
        collapsed={collapsed}
        count={runningSeries ?? undefined}
        testId="sidebar-series"
      />}

      {show("your-move") && <SidebarItem
        label={t("shell.nav.yourMove")}
        href={ticketBucketHref("needs-you")}
        icon={Inbox}
        active={yourMoveActive}
        collapsed={collapsed}
        count={yourMoves}
        attention
        onClick={chooseTickets("needs-you")}
        testId="sidebar-your-move"
      />}
      {show("tickets") && <SidebarItem
        label={t("shell.nav.tickets")}
        href={TICKETS_PATH}
        icon={Ticket}
        active={under(TICKETS_PATH) && !yourMoveActive}
        collapsed={collapsed}
        count={tickets ? totalTickets(tickets) : undefined}
        onClick={chooseTickets("all")}
        testId="sidebar-tickets"
      />}
      {show("runs") && <SidebarItem
        label={t("shell.nav.runs")}
        href={bucketHref("all")}
        icon={CirclePlay}
        active={under(RUNS_PATH) || under("/jobs")}
        collapsed={collapsed}
        count={inFlight}
        onClick={chooseRuns}
        testId="sidebar-runs"
      />}
      {show("pull-requests") && <SidebarItem
        label={t("shell.nav.pullRequests")}
        href="/pull-requests"
        icon={GitPullRequest}
        active={under("/pull-requests")}
        collapsed={collapsed}
        count={openPrs ?? undefined}
        testId="sidebar-pull-requests"
      />}
      {/* 2026-11-23-8df6a: what merged and what agent smith deployed, right below Pull requests (PoCs follow it). */}
      {show("deployments") && <SidebarItem
        label={t("deployments.nav")}
        href="/deployments"
        icon={Rocket}
        active={under("/deployments")}
        collapsed={collapsed}
        testId="sidebar-deployments"
      />}

      {/* 2026-11-23-933da: Pull requests, Deployments (2026-11-23-8df6a), then PoCs. */}
      {show("pocs") && <SidebarItem label={t("pocs.nav")} href="/pocs" icon={LayoutGrid} active={under("/pocs")} collapsed={collapsed} testId="sidebar-pocs" />}

      {show("insight") && <SidebarGroup label={t("shell.nav.insight")} collapsed={collapsed} />}
      {show("overview") && <SidebarItem label={t("shell.nav.overview")} href="/overview" icon={ChartColumn} active={under("/overview")} collapsed={collapsed} testId="sidebar-overview" />}
      {show("delivery") && <SidebarItem label={t("shell.nav.delivery")} href="/delivery" icon={CalendarDays} active={under("/delivery")} collapsed={collapsed} testId="sidebar-delivery" />}

      {show("recent") && recent.length > 0 && <SidebarGroup label={t("shell.nav.recent")} collapsed={collapsed} />}
      {show("recent") && recent.map((place) => (
        <SidebarItem
          key={place.href}
          label={place.label}
          href={place.href}
          icon={Clock}
          active={pathname === place.href}
          collapsed={collapsed}
          testId={`sidebar-recent-${place.href}`}
        />
      ))}

      <ProfileMenu collapsed={collapsed} />
    </nav>
  );
}
