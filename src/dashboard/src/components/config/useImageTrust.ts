"use client";

import { useEffect, useState } from "react";
import { fetchImageTrust, type ImageTrustBoundary } from "@/lib/configApi";

// 2026-10-12-6bfab: the sandbox image boundary in force, as the server computes it. Refetched
// whenever `refreshKey` changes — the Sandbox form keys it on the PERSISTED boundary fields, so
// a save shows the boundary the next decision will use.

export interface UseImageTrust {
  boundary: ImageTrustBoundary | null;
  error: Error | null;
}

export function useImageTrust(refreshKey: string): UseImageTrust {
  const [boundary, setBoundary] = useState<ImageTrustBoundary | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const abort = new AbortController();
    fetchImageTrust(abort.signal)
      .then((next) => {
        setBoundary(next);
        setError(null);
      })
      .catch((err: Error) => {
        if (!abort.signal.aborted) setError(err);
      });
    return () => abort.abort();
  }, [refreshKey]);

  return { boundary, error };
}
