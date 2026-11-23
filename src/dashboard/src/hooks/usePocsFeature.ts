"use client";

import { useEffect, useState } from "react";
import { useAccessToken } from "@/hooks/useAccessToken";
import { fetchPocs, type PocList } from "@/lib/pocsApi";

// 2026-11-23-933da: whether the PoCs area exists for this caller, read from GET /api/pocs itself — on (with the
// answer), off (pocs.enabled is false), forbidden (no pocs.read), or unknown while the read is out or failed. Read
// again when the token changes: a sign-in is a different caller.

export type PocsStatus = "on" | "off" | "forbidden" | "unknown";

export interface PocsFeature {
  status: PocsStatus;
  list: PocList | null;
  /** The last read failed for a reason other than the three answers; status is then unknown. */
  failed: boolean;
}

export function usePocsFeature(): PocsFeature {
  const token = useAccessToken();
  const [status, setStatus] = useState<PocsStatus>("unknown");
  const [list, setList] = useState<PocList | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    fetchPocs(controller.signal)
      .then((read) => {
        if (controller.signal.aborted) return;
        setStatus(read.status);
        setFailed(false);
        setList(read.status === "on" ? read.list : null);
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        setStatus("unknown");
        setFailed(true);
      });
    return () => controller.abort();
  }, [token]);
  return { status, list, failed };
}
