"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import { fetchApprovalPreview } from "@/lib/ticketsApi";
import type { TicketApprovalPreview } from "@/types/tickets";
import { CoalescedRefetch } from "./CoalescedRefetch";
import type { Read } from "./TicketPageReads";

// 2026-10-13-9a59r: what approving a ticket's open proposal would do — the one read this spec adds.
// It reads the tracker per request, so it is asked only while a proposal is open (`enabled`) and asked
// again only when `nudge` changes: on the ticket page a TicketActivityChanged for this ticket or a moved
// newest-attempt status, in Work it out a new proposal. Never more than one fetch per 2 s.

export const APPROVAL_PREVIEW_MS = 2000;

export interface ApprovalPreviewOptions {
  enabled: boolean;
  /** Any change re-reads the preview (coalesced). */
  nudge: string;
}

const IDLE: Read<TicketApprovalPreview> = { data: null, failed: false, refusal: null, loading: false };

export function useApprovalPreview(
  project: string | null, ticketId: string | null, { enabled, nudge }: ApprovalPreviewOptions,
): Read<TicketApprovalPreview> {
  const [read, setRead] = useState<Read<TicketApprovalPreview>>(IDLE);
  const active = enabled && project !== null && ticketId !== null;
  const fire = useRef<() => void>(() => {});
  const tick = useMemo(() => new CoalescedRefetch(() => fire.current()), []);

  useEffect(() => {
    if (!active || project === null || ticketId === null) {
      setRead(IDLE);
      return;
    }
    const controller = new AbortController();
    let lastFetch = 0;
    const load = () => {
      lastFetch = Date.now();
      fetchApprovalPreview(project, ticketId, controller.signal)
        .then((data) => {
          if (!controller.signal.aborted) setRead({ data, failed: false, refusal: null, loading: false });
        })
        .catch((thrown: unknown) => {
          if (controller.signal.aborted) return;
          const refusal = refusalIn(thrown);
          const notFound = thrown instanceof ApiResponseError && thrown.status === 404;
          setRead({ data: null, failed: refusal === null && !notFound, refusal, loading: false, notFound });
        });
    };
    fire.current = () => {
      const wait = lastFetch + APPROVAL_PREVIEW_MS - Date.now();
      if (wait > 0) tick.within(wait);
      else load();
    };
    setRead((held) => (held.data ? held : { ...IDLE, loading: true }));
    load();
    return () => {
      controller.abort();
      tick.cancel();
      fire.current = () => {};
    };
  }, [active, project, ticketId, tick]);

  const seen = useRef(nudge);
  useEffect(() => {
    if (seen.current === nudge) return;
    seen.current = nudge;
    if (active) tick.within(APPROVAL_PREVIEW_MS);
  }, [nudge, active, tick]);

  return active ? read : IDLE;
}
