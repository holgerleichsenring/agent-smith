"use client";

import { useSyncExternalStore } from "react";
import { chooseShowSystemRuns, currentShowSystemRuns, subscribeShowSystemRuns } from "@/lib/runs/showSystemRuns";

// 2026-11-24-35ff: the prerendered shell has no storage, so the server snapshot hides system runs; the page reads the
// viewer's choice once it hydrates.

export function useShowSystemRuns(): [boolean, (show: boolean) => void] {
  const show = useSyncExternalStore(subscribeShowSystemRuns, currentShowSystemRuns, () => false);
  return [show, chooseShowSystemRuns];
}
