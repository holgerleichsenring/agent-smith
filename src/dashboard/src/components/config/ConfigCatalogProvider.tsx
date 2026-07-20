"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { fetchChanges } from "@/lib/configApi";
import { useConfigCatalog, type UseConfigCatalog } from "./useConfigCatalog";

// p0353: ONE shared catalog instance for the whole config surface. The left rail
// (AppRail's ConfigRailSections) and the studio content pane used to call
// useConfigCatalog() independently, so an import/save/revert only refreshed the
// pane that ran `reload()` — the rail's count badges stayed stale until a
// remount. This provider lifts the single instance (plus the History "Changes"
// count) above both subtrees in the shell layout, so one `reload()` refreshes
// BOTH panes at once. Mirrors useCapabilities' single-source-of-truth intent,
// but as reactive context because reload() must re-render every consumer.

interface ConfigCatalogContextValue extends UseConfigCatalog {
  /** Number of audit-log entries — the rail's History "Changes" badge. Null while
   *  loading or on failure (the badge renders no count then, never a fake 0). */
  changesCount: number | null;
}

const ConfigCatalogContext = createContext<ConfigCatalogContextValue | null>(null);

export function ConfigCatalogProvider({ children }: { children: React.ReactNode }) {
  const { catalog, loading, error, reload: reloadCatalog } = useConfigCatalog();
  const [changesCount, setChangesCount] = useState<number | null>(null);

  const loadChanges = useCallback(
    (signal?: AbortSignal) =>
      fetchChanges(signal)
        .then((changes) => setChangesCount(changes.length))
        .catch(() => {
          /* honest: no count rather than a fabricated 0 */
        }),
    [],
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadChanges(controller.signal);
    return () => controller.abort();
  }, [loadChanges]);

  // One reload() refreshes the catalog AND the changes count, so an import or a
  // revert updates the rail's per-kind badges and the History count together.
  const reload = useCallback(async () => {
    await Promise.all([reloadCatalog(), loadChanges()]);
  }, [reloadCatalog, loadChanges]);

  const value = useMemo<ConfigCatalogContextValue>(
    () => ({ catalog, loading, error, reload, changesCount }),
    [catalog, loading, error, reload, changesCount],
  );

  return <ConfigCatalogContext.Provider value={value}>{children}</ConfigCatalogContext.Provider>;
}

export function useConfigCatalogContext(): ConfigCatalogContextValue {
  const ctx = useContext(ConfigCatalogContext);
  if (!ctx) {
    throw new Error("useConfigCatalogContext must be used within a <ConfigCatalogProvider>");
  }
  return ctx;
}
