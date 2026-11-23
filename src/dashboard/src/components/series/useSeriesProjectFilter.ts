"use client";

import { useCallback, useEffect, useState } from "react";

// 2026-11-23-31dcc: the series list's project, remembered per browser; read after mount, because the prerendered shell
// has no storage. Empty is every project; blocked storage keeps the choice for this page only.

const KEY = "agentsmith.series-project";

/** The project, the chooser, and whether the stored choice has been read — the list waits for it, so a remembered
 *  project is the first read. */
export function useSeriesProjectFilter(): [string, (project: string) => void, boolean] {
  const [project, setProject] = useState("");
  const [ready, setReady] = useState(false);
  useEffect(() => {
    try {
      setProject(window.localStorage.getItem(KEY) ?? "");
    } catch {
      /* every project */
    }
    setReady(true);
  }, []);
  const choose = useCallback((next: string) => {
    setProject(next);
    try {
      window.localStorage.setItem(KEY, next);
    } catch {
      /* remembered for this page only */
    }
  }, []);
  return [project, choose, ready];
}
