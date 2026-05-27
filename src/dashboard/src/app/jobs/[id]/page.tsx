"use client";

import { use, useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams, useRouter, usePathname } from "next/navigation";
import { useJobsHub } from "@/hooks/useJobsHub";
import { useRunEvents } from "@/hooks/useRunEvents";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { TopologyCard } from "@/components/jobs/TopologyCard";
import { SandboxList } from "@/components/jobs/SandboxList";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function RunDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const runId = decodeURIComponent(id);
  const { connectionState, overview } = useJobsHub();
  const events = useRunEvents(runId);

  const snapshot = useMemo(() => {
    if (!overview) return null;
    return overview.active.find((r) => r.runId === runId)
      ?? overview.recent.find((r) => r.runId === runId)
      ?? null;
  }, [overview, runId]);

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const expandedFromUrl = useMemo(() => parseExpandParam(searchParams.get("expand")), [searchParams]);
  const [expanded, setExpanded] = useState<Set<string>>(expandedFromUrl);

  useEffect(() => {
    setExpanded(expandedFromUrl);
    // intentionally not in deps: only reset when URL changes
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

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <header className="flex items-start justify-between gap-4">
        <div className="space-y-1">
          <Link href="/" className="text-xs text-stone-500 hover:underline">← runs</Link>
          <h1 className="text-2xl font-medium tracking-tight">{snapshot?.pipeline ?? "run"}</h1>
        </div>
        <ConnectionState state={connectionState} />
      </header>
      <TopologyCard runId={runId} snapshot={snapshot} events={events} />
      <SandboxList runId={runId} events={events} expanded={expanded} onToggle={toggle} />
    </main>
  );
}

function parseExpandParam(raw: string | null): Set<string> {
  if (!raw) return new Set();
  return new Set(raw.split(",").map((s) => s.trim()).filter((s) => s.length > 0));
}
