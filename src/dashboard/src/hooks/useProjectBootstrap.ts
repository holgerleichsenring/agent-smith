"use client";

import { useEffect, useState } from "react";
import { fetchProjectBootstrap, type RepoBootstrap } from "@/lib/projectBootstrapApi";

// 2026-10-13-0b82c: one read per project shown — every repository row takes its entry from it, so
// a project of N repositories costs one request and N host reads, not N of each. Null until the
// server answers, and stays null when it cannot (no permission, no server): the rows show nothing.
export function useProjectBootstrap(project: string): Map<string, RepoBootstrap> | null {
  const [entries, setEntries] = useState<Map<string, RepoBootstrap> | null>(null);

  useEffect(() => {
    const ctrl = new AbortController();
    setEntries(null);
    fetchProjectBootstrap(project, ctrl.signal)
      .then((repos) => {
        if (!ctrl.signal.aborted) setEntries(new Map(repos.map((r) => [r.repo, r])));
      })
      .catch(() => undefined);
    return () => ctrl.abort();
  }, [project]);

  return entries;
}
