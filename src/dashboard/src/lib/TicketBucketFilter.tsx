"use client";

import { useRouter } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { TICKET_BUCKETS, type TicketBucket } from "@/types/tickets";

// 2026-10-13-9a59p: the ticket list's bucket, the twin of RunBucketFilter — in the URL so a bucket
// view is linkable, shared by the rail's highlight and the list's sections, and read through the
// History API because its provider sits in the root layout, where a search-param hook fails the
// static build.

/** "all" is the unfiltered list — every section. */
export type TicketBucketFilter = TicketBucket | "all";

const PARAM = "bucket";
export const TICKETS_PATH = "/tickets";

export function ticketBucketHref(filter: TicketBucketFilter): string {
  return filter === "all" ? TICKETS_PATH : `${TICKETS_PATH}?${PARAM}=${filter}`;
}

export function parseTicketBucket(search: string): TicketBucketFilter {
  const value = new URLSearchParams(search).get(PARAM);
  return TICKET_BUCKETS.includes(value as TicketBucket) ? (value as TicketBucket) : "all";
}

interface TicketBucketFilterValue {
  filter: TicketBucketFilter;
  select: (filter: TicketBucketFilter) => void;
}

const Ctx = createContext<TicketBucketFilterValue>({ filter: "all", select: () => {} });

export function TicketBucketFilterProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [filter, setFilter] = useState<TicketBucketFilter>("all");

  // Seeded after mount: the prerendered shell has no URL. popstate keeps back/forward honest.
  useEffect(() => {
    const readUrl = () => setFilter(parseTicketBucket(window.location.search));
    readUrl();
    window.addEventListener("popstate", readUrl);
    return () => window.removeEventListener("popstate", readUrl);
  }, []);

  const select = useCallback(
    (next: TicketBucketFilter) => {
      setFilter(next);
      if (window.location.pathname === TICKETS_PATH) {
        window.history.pushState(null, "", ticketBucketHref(next));
      } else {
        router.push(ticketBucketHref(next));
      }
    },
    [router],
  );

  const value = useMemo(() => ({ filter, select }), [filter, select]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useTicketBucketFilter(): TicketBucketFilterValue {
  return useContext(Ctx);
}
