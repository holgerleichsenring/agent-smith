"use client";

import { useCallback, useEffect, useState } from "react";

// 2026-11-16-0440c: whether the sidebar shows icons only, remembered per browser. Read after
// mount, because the prerendered shell has no storage; blocked storage leaves it wide.

const KEY = "agentsmith.sidebar-collapsed";

export function useSidebarCollapsed(): [boolean, () => void] {
  const [collapsed, setCollapsed] = useState(false);
  useEffect(() => {
    try {
      setCollapsed(window.localStorage.getItem(KEY) === "1");
    } catch {
      /* stays wide */
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
