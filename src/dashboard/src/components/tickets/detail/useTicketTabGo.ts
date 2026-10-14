"use client";

import { useCallback } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";

// 2026-10-13-9a59r: the ticket page's address beyond the route — ?tab=, the selected spec (?spec=) and
// the attempt whose cause the Conversation tab scrolls to (?causeOf=). Every cross-link (Continue in
// conversation, Open specs, Open the spec, Discuss in conversation) goes through go().

export const TAB_PARAM = "tab";
export const SPEC_PARAM = "spec";
export const CAUSE_PARAM = "causeOf";

export interface TicketAddress {
  param: (name: string) => string | null;
  /** Selects a tab; `extra` sets (a string) or clears (null) further parameters. */
  go: (tab: string, extra?: Record<string, string | null>) => void;
}

export function useTicketTabGo(): TicketAddress {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname() ?? TICKETS_PATH;
  const search = params?.toString() ?? "";
  const go = useCallback((tab: string, extra: Record<string, string | null> = {}) => {
    const next = new URLSearchParams(search);
    next.set(TAB_PARAM, tab);
    for (const [name, value] of Object.entries(extra)) {
      if (value === null) next.delete(name);
      else next.set(name, value);
    }
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  }, [search, router, pathname]);
  const param = useCallback((name: string) => new URLSearchParams(search).get(name), [search]);
  return { param, go };
}
