"use client";

import { use, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { EventFilterProvider } from "@/lib/EventFilterContext";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { FilterRail } from "@/components/jobs/FilterRail";
import { TopologyCard } from "@/components/jobs/TopologyCard";
import { RunToolsPanel } from "@/components/jobs/RunToolsPanel";
import { SandboxList } from "@/components/jobs/SandboxList";
import { TrailTab } from "@/components/jobs/TrailTab";
import { ActivityTab } from "@/components/jobs/ActivityTab";
import { EventType } from "@/types/hub-events";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const runId = decodeURIComponent(id);
  return (
    <EventFilterProvider>
      <RunDetail runId={runId} />
    </EventFilterProvider>
  );
}

function RunDetail({ runId }: { runId: string }) {
  const { connectionState, overview } = useJobsHub();
  const events = useRunEvents(runId);

  const snapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);

  const repoNames = useMemo(() => {
    const repos = new Set<string>();
    for (const e of events) if (e.type === EventType.SandboxCreated) repos.add(e.repo);
    return [...repos].sort();
  }, [events]);

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const expandedFromUrl = useMemo(() => parseExpandParam(searchParams.get("expand")), [searchParams]);
  const [expanded, setExpanded] = useState<Set<string>>(expandedFromUrl);

  useEffect(() => {
    setExpanded(expandedFromUrl);
  }, [expandedFromUrl]);

  const updateUrl = useCallback((next: Set<string>) => {
    const params = new URLSearchParams(searchParams.toString());
    if (next.size === 0) params.delete("expand");
    else params.set("expand", [...next].join(","));
    const qs = params.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  }, [pathname, router, searchParams]);

  const toggle = useCallback((repo: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(repo)) next.delete(repo);
      else next.add(repo);
      updateUrl(next);
      return next;
    });
  }, [updateUrl]);

  const expandAll = useCallback(() => {
    const next = new Set(repoNames);
    setExpanded(next);
    updateUrl(next);
  }, [repoNames, updateUrl]);

  const collapseAll = useCallback(() => {
    setExpanded(new Set());
    updateUrl(new Set());
  }, [updateUrl]);

  const tabParam = searchParams.get("tab");
  const activeTab: "topology" | "trail" | "activity" =
    tabParam === "trail" ? "trail" : tabParam === "activity" ? "activity" : "topology";
  const setActiveTab = useCallback((tab: "topology" | "trail" | "activity") => {
    const params = new URLSearchParams(searchParams.toString());
    if (tab === "topology") params.delete("tab");
    else params.set("tab", tab);
    const qs = params.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  }, [pathname, router, searchParams]);

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <Link href="/" className="text-xs text-stone-500 hover:underline">← runs</Link>
          <h1 className="text-2xl font-medium tracking-tight">{snapshot?.pipeline ?? "run"}</h1>
        </div>
        <ConnectionState state={connectionState} />
      </header>
      <nav className="flex gap-4 border-b border-stone-200 text-sm">
        <button
          type="button"
          onClick={() => setActiveTab("topology")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "topology" ? "border-stone-800 text-stone-800" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-topology"
        >Topology</button>
        <button
          type="button"
          onClick={() => setActiveTab("activity")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "activity" ? "border-stone-800 text-stone-800" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-activity"
        >Activity</button>
        <button
          type="button"
          onClick={() => setActiveTab("trail")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "trail" ? "border-stone-800 text-stone-800" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-trail"
        >Trail</button>
      </nav>
      {activeTab === "trail" ? (
        <TrailTab runId={runId} />
      ) : activeTab === "activity" ? (
        <ActivityTab runId={runId} />
      ) : (
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[180px_minmax(0,1fr)]">
        <FilterRail />
        <div className="space-y-6">
          <TopologyCard runId={runId} snapshot={snapshot} events={events} />
          <SandboxList
            runId={runId}
            events={events}
            expanded={expanded}
            onToggle={toggle}
            onExpandAll={expandAll}
            onCollapseAll={collapseAll}
          />
          <RunToolsPanel events={events} />
        </div>
      </div>
      )}
    </main>
  );
}

function parseExpandParam(raw: string | null): Set<string> {
  if (!raw) return new Set();
  return new Set(raw.split(",").map((s) => s.trim()).filter((s) => s.length > 0));
}
