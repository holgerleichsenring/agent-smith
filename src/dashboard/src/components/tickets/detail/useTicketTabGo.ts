"use client";

import { useCallback } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { TICKETS_PATH } from "@/lib/TicketBucketFilter";
import { DEFAULT_SECTION, SECTION_PARAM, TAB_PARAM, sectionIdOf } from "./ticketSectionNames";

// 2026-10-13-9a59r: the ticket page's address beyond the route — the section, the selected spec (?spec=) and the
// attempt whose cause the Conversation scrolls to (?causeOf=). Every cross-link (read, Open specs, Open the spec,
// Discuss) goes through go(). 2026-11-17-8d1ca: go() writes ?section= and drops the older ?tab=; a name the section
// table does not know (an old tab id such as "attempts") is written as the section it lands on. 2026-11-22-4321a:
// the default section, left out of the query, is Now.

export { SECTION_PARAM, TAB_PARAM } from "./ticketSectionNames";
export const SPEC_PARAM = "spec";
export const CAUSE_PARAM = "causeOf";

export interface TicketAddress {
  param: (name: string) => string | null;
  /** Shows a section, or Now (null); `extra` sets (a string) or clears (null) further parameters. */
  go: (section: string | null, extra?: Record<string, string | null>) => void;
}

/** The query a go() writes: the section first (Now is the default and left out), never ?tab=, then the
 *  other parameters as they were with `extra` applied. */
export function addressQuery(search: string, section: string | null, extra: Record<string, string | null> = {}): string {
  const next = new URLSearchParams();
  const name = sectionIdOf(section);
  if (name !== DEFAULT_SECTION) next.set(SECTION_PARAM, name);
  for (const [key, value] of new URLSearchParams(search)) {
    if (key !== TAB_PARAM && key !== SECTION_PARAM) next.append(key, value);
  }
  for (const [key, value] of Object.entries(extra)) {
    if (value === null) next.delete(key);
    else next.set(key, value);
  }
  return next.toString();
}

export function useTicketTabGo(): TicketAddress {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname() ?? TICKETS_PATH;
  const search = params?.toString() ?? "";
  const go = useCallback((section: string | null, extra: Record<string, string | null> = {}) => {
    const query = addressQuery(search, section, extra);
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }, [search, router, pathname]);
  const param = useCallback((name: string) => new URLSearchParams(search).get(name), [search]);
  return { param, go };
}
