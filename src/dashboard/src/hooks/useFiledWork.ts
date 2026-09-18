"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { HUB_URL } from "@/hooks/useJobsHub";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { fetchFiledWork } from "@/lib/specDialogApi";
import type { FiledWork, SpecDialogFilingPush } from "@/types/spec-dialog";

// 2026-09-17-042ej: what the conversation filed, as its runs stand now. The read is its own
// route and the hub only nudges: the page fetches on connect, refetches on the nudge, and
// survives a reload because none of this lives in a push.
//
// The WATCH is re-issued whenever the filing changes, because the server reads the ticket ids
// off the session's latest filing — a watch registered before the filing follows nothing. It is
// re-issued on RECONNECT for a harder reason: a reconnected connection has a fresh id and the
// server-side registry is keyed by connection, so the old watch belongs to a connection that no
// longer exists. The refetch goes with it, because every nudge sent during the drop is gone —
// a run whose last phase finished while the socket was down would otherwise read as running
// until somebody reloaded the page.

// The run list's own window, for the same reason: a busy run nudges many times a second and
// a refetch per nudge would be a wall of mutually-cancelling requests.
const NUDGE_COALESCE_MS = 350;

export function useFiledWork(
  dialogId: string | null,
  filed: SpecDialogFilingPush | null,
): FiledWork | null {
  const [work, setWork] = useState<FiledWork | null>(null);
  // Reads overlap — a nudge fires one while the filing's own is still out — so the sequence
  // number says which answer is still the latest.
  const reads = useRef(0);

  const load = useCallback(async (id: string) => {
    const issued = (reads.current += 1);
    try {
      const next = await fetchFiledWork(id);
      if (issued === reads.current) setWork(next);
    } catch (thrown) {
      // The filed tab is beside the conversation, not the conversation: a failed read of it
      // must not put a page-wide failure over a dialog that is working.
      console.warn("the filed work could not be read", thrown);
    }
  }, []);

  useEffect(() => {
    if (!dialogId) return;
    const client = getJobsHubClient(HUB_URL);
    let cancelled = false;
    let coalesce: ReturnType<typeof setTimeout> | null = null;
    let stop: (() => Promise<void>) | null = null;

    const schedule = () => {
      if (coalesce !== null) return;
      coalesce = setTimeout(() => {
        coalesce = null;
        if (!cancelled) void load(dialogId);
      }, NUDGE_COALESCE_MS);
    };

    const watch = () =>
      client.watchFiledWork(dialogId)
        .then((cancel) => {
          if (cancelled) return void cancel();
          stop = cancel;
        })
        // A caller who may not follow runs still gets the conversation; the tab stays empty.
        .catch((thrown) => console.warn("the filed work could not be watched", thrown));

    const offNudge = client.filedWorkChanged.add(schedule);
    const offConnection = client.connectionState.add((state) => {
      if (state !== HubConnectionState.Connected || cancelled) return;
      void watch();
      void load(dialogId);
    });
    void watch();
    void load(dialogId);

    return () => {
      cancelled = true;
      if (coalesce !== null) clearTimeout(coalesce);
      offNudge();
      offConnection();
      void stop?.();
    };
    // `filed` is a dependency on purpose: a new filing changes BOTH what the server should
    // watch and what the read answers, and the watch is what has to be re-issued.
  }, [dialogId, filed, load]);

  return work;
}
