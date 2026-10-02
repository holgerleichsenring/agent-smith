"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { fetchConnectionRepos, refreshConnectionDiscovery, type ConnectionRepos } from "@/lib/configApi";

// 2026-10-02-5f89c: one connection's discovery as the server keeps it — read once per connection,
// and read AGAIN after Refresh now, so what shows is what every replica now answers. A re-read
// keeps the last answer on screen; only a new connection starts from "loading".

export function useConnectionDiscovery(connectionId: string) {
  const [discovered, setDiscovered] = useState<ConnectionRepos | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshError, setRefreshError] = useState<Error | null>(null);
  const [reads, setReads] = useState(0);
  const shownFor = useRef<string | null>(null);

  useEffect(() => {
    if (shownFor.current !== connectionId) {
      shownFor.current = connectionId;
      setDiscovered(null);
      setRefreshError(null);
      setLoading(connectionId !== "");
    }
    setError(null);
    if (!connectionId) return;
    const controller = new AbortController();
    fetchConnectionRepos(connectionId, controller.signal)
      .then((r) => setDiscovered(r))
      .catch((err: Error) => {
        if (err.name !== "AbortError") setError(err);
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [connectionId, reads]);

  const refresh = useCallback(() => {
    if (!connectionId) return;
    setRefreshing(true);
    setRefreshError(null);
    refreshConnectionDiscovery(connectionId)
      .then(() => setReads((n) => n + 1))
      .catch((err: Error) => setRefreshError(err))
      .finally(() => setRefreshing(false));
  }, [connectionId]);

  return { discovered, error, loading, refreshing, refreshError, refresh };
}
