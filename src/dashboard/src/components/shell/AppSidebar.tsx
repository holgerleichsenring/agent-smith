"use client";

import { useMemo } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  CalendarDays, ChartColumn, CirclePlay, Clock, GitPullRequest, Inbox, PanelLeft, Plus, Ticket, Terminal,
} from "lucide-react";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useOpenPrCount } from "@/hooks/useOpenPrCount";
import { useRecentPlaces } from "@/hooks/useRecentPlaces";
import { useSidebarCollapsed } from "@/hooks/useSidebarCollapsed";
import { bucketRuns } from "@/components/jobs/mission/missionBuckets";
import { totalTickets } from "@/components/tickets/ticketSections";
import { mergeNewestFirst } from "@/lib/runs/mergeNewestFirst";
import { bucketHref, RUNS_PATH, useRunBucketFilter } from "@/lib/RunBucketFilter";
import { TICKETS_PATH, ticketBucketHref, useTicketBucketFilter, type TicketBucketFilter } from "@/lib/TicketBucketFilter";
import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { hasSectionBar } from "@/lib/sectionBarPage";
import { cn } from "@/lib/utils";
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
  const ticketFilter = useTicketBucketFilter();
  const runFilter = useRunBucketFilter();
  const { overview } = useJobsHub();
  const openPrs = useOpenPrCount();
  const recent = useRecentPlaces(pathname);

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
    <nav aria-label="Main" data-testid="app-sidebar" data-collapsed={collapsed ? "true" : "false"} className={cn("app-sidebar", collapsed && "collapsed")}>
      <div className="sb-brand">
        <Terminal size={20} strokeWidth={2} aria-hidden="true" />
        {!collapsed && <span className="sb-brand-name">Agent Smith</span>}
        {!ownBar && <button
          type="button"
          className="sb-toggle"
          onClick={toggle}
          aria-label={collapsed ? "Expand the sidebar" : "Collapse the sidebar"}
          title={collapsed ? "Expand the sidebar" : "Collapse the sidebar"}
          data-testid="sidebar-toggle"
        >
          <PanelLeft size={16} strokeWidth={1.8} aria-hidden="true" />
        </button>}
      </div>

      <Link
        href="/spec-dialog"
        className={cn("sb-new", under("/spec-dialog") && "on")}
        title="New work"
        aria-label={collapsed ? "New work" : undefined}
        aria-current={under("/spec-dialog") ? "page" : undefined}
        data-testid="sidebar-new-work"
      >
        <Plus size={16} strokeWidth={2.2} aria-hidden="true" />
        {!collapsed && <span>New work</span>}
      </Link>

      <SidebarItem
        label="Your move"
        href={ticketBucketHref("needs-you")}
        icon={Inbox}
        active={yourMoveActive}
        collapsed={collapsed}
        count={tickets?.needsYou}
        attention
        onClick={chooseTickets("needs-you")}
        testId="sidebar-your-move"
      />
      <SidebarItem
        label="Tickets"
        href={TICKETS_PATH}
        icon={Ticket}
        active={under(TICKETS_PATH) && !yourMoveActive}
        collapsed={collapsed}
        count={tickets ? totalTickets(tickets) : undefined}
        onClick={chooseTickets("all")}
        testId="sidebar-tickets"
      />
      <SidebarItem
        label="Runs"
        href={bucketHref("all")}
        icon={CirclePlay}
        active={under(RUNS_PATH) || under("/jobs")}
        collapsed={collapsed}
        count={inFlight}
        onClick={chooseRuns}
        testId="sidebar-runs"
      />
      <SidebarItem
        label="Pull requests"
        href="/pull-requests"
        icon={GitPullRequest}
        active={under("/pull-requests")}
        collapsed={collapsed}
        count={openPrs ?? undefined}
        testId="sidebar-pull-requests"
      />

      <SidebarGroup label="Insight" collapsed={collapsed} />
      <SidebarItem label="Overview" href="/overview" icon={ChartColumn} active={under("/overview")} collapsed={collapsed} testId="sidebar-overview" />
      <SidebarItem label="Delivery" href="/delivery" icon={CalendarDays} active={under("/delivery")} collapsed={collapsed} testId="sidebar-delivery" />

      {recent.length > 0 && <SidebarGroup label="Recent" collapsed={collapsed} />}
      {recent.map((place) => (
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
