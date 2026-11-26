"use client";

import { useCallback, useEffect, useState } from "react";

// 2026-11-26-b186: whether the series graph is folded to one line, remembered per browser like the sidebar
// (useSidebarCollapsed): read after mount, because the prerendered page has no storage; blocked storage leaves it open.

const KEY = "agentsmith.series-graph-collapsed";

export function useSeriesGraphCollapsed(): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState(false);
  useEffect(() => {
    try {
      setCollapsed(window.localStorage.getItem(KEY) === "1");
    } catch {
      /* stays open */
    }
  }, []);
  const toggle = useCallback(() => {
    setCollapsed((was) => {
      try {
        window.localStorage.setItem(KEY, was ? "0" : "1");
      } catch {
        /* remembered for this page only */
      }
      return !was;
    });
  }, []);
  return [collapsed, toggle];
}
