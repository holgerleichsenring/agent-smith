"use client";

import { use, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { EventFilterProvider } from "@/lib/EventFilterContext";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { TopologyCard } from "@/components/jobs/TopologyCard";
import { RunToolsPanel } from "@/components/jobs/RunToolsPanel";
import { TrailTab } from "@/components/jobs/TrailTab";
import { ActivityTab } from "@/components/jobs/ActivityTab";
import { ResultTab } from "@/components/jobs/ResultTab";
import { TopologyGraph } from "@/components/jobs/TopologyGraph";
import { TopologyDetail } from "@/components/jobs/TopologyDetail";
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

  // p0169j-d: single-select selection for the Topology graph + detail
  // pane. URL ?expand=a,b,c keeps backwards-compat — last value wins
  // (the multi-expand semantic is gone with the SVG topology view).
  const selectedFromUrl = useMemo(
    () => lastExpandParam(searchParams.get("expand")),
    [searchParams],
  );
  const [selectedTopologyRepo, setSelectedTopologyRepo] = useState<string | null>(selectedFromUrl);

  useEffect(() => {
    setSelectedTopologyRepo(selectedFromUrl);
  }, [selectedFromUrl]);

  const updateUrl = useCallback((next: string | null) => {
    const params = new URLSearchParams(searchParams.toString());
    if (next === null) params.delete("expand");
    else params.set("expand", next);
    const qs = params.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  }, [pathname, router, searchParams]);

  const selectTopologyRepo = useCallback((repo: string) => {
    setSelectedTopologyRepo((prev) => {
      const next = prev === repo ? null : repo;
      updateUrl(next);
      return next;
    });
  }, [updateUrl]);
  void repoNames;

  const tabParam = searchParams.get("tab");
  const activeTab: "topology" | "trail" | "activity" | "result" =
    tabParam === "trail" ? "trail"
      : tabParam === "activity" ? "activity"
      : tabParam === "result" ? "result"
      : "topology";
  const setActiveTab = useCallback((tab: "topology" | "trail" | "activity" | "result") => {
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
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "topology" ? "border-[var(--color-primary)] text-[var(--color-ink)]" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-topology"
        >Topology</button>
        <button
          type="button"
          onClick={() => setActiveTab("activity")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "activity" ? "border-[var(--color-primary)] text-[var(--color-ink)]" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-activity"
        >Activity</button>
        <button
          type="button"
          onClick={() => setActiveTab("result")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "result" ? "border-[var(--color-primary)] text-[var(--color-ink)]" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-result"
        >Result</button>
        <button
          type="button"
          onClick={() => setActiveTab("trail")}
          className={`-mb-px border-b-2 px-2 py-2 ${activeTab === "trail" ? "border-[var(--color-primary)] text-[var(--color-ink)]" : "border-transparent text-stone-500 hover:text-stone-700"}`}
          data-testid="tab-trail"
        >Trail</button>
      </nav>
      {activeTab === "trail" ? (
        <TrailTab
          runId={runId}
          isFinished={snapshot?.finishedAt !== null && snapshot?.finishedAt !== undefined}
          prUrl={snapshot?.prUrl ?? null}
        />
      ) : activeTab === "activity" ? (
        <ActivityTab runId={runId} />
      ) : activeTab === "result" ? (
        <ResultTab runId={runId} prUrl={snapshot?.prUrl ?? null} />
      ) : (
      <div className="space-y-6">
        <TopologyCard runId={runId} snapshot={snapshot} events={events} />
        <TopologyGraph
          pipeline={snapshot?.pipeline ?? null}
          runId={runId}
          events={events}
          selected={selectedTopologyRepo}
          onSelect={selectTopologyRepo}
        />
        <TopologyDetail runId={runId} selected={selectedTopologyRepo} />
        <RunToolsPanel events={events} />
      </div>
      )}
    </main>
  );
}

function lastExpandParam(raw: string | null): string | null {
  if (!raw) return null;
  const tokens = raw.split(",").map((s) => s.trim()).filter((s) => s.length > 0);
  return tokens.length === 0 ? null : tokens[tokens.length - 1];
}
