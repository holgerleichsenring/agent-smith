"use client";

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  EventFilterState,
  defaultFilterState,
  parseFilterFromQuery,
  writeFilterToQuery,
} from "./eventFilterQuery";
import type { EventType } from "@/types/hub-events";

interface EventFilterContextValue {
  state: EventFilterState;
  toggle: (level: "l1" | "l2" | "l3", type: EventType) => void;
  setLevel: (level: "l1" | "l2" | "l3", types: ReadonlySet<EventType>) => void;
}

const Ctx = createContext<EventFilterContextValue | null>(null);

export function EventFilterProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const seedFromUrl = useMemo(
    () => parseFilterFromQuery(new URLSearchParams(searchParams.toString())),
    [searchParams],
  );
  const [state, setState] = useState<EventFilterState>(seedFromUrl);

  const persist = useCallback((next: EventFilterState) => {
    const params = writeFilterToQuery(next, new URLSearchParams(searchParams.toString()));
    const qs = params.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  }, [pathname, router, searchParams]);

  const toggle = useCallback((level: "l1" | "l2" | "l3", type: EventType) => {
    setState((prev) => {
      const next: EventFilterState = {
        l1: new Set(prev.l1),
        l2: new Set(prev.l2),
        l3: new Set(prev.l3),
      };
      const set = next[level] as Set<EventType>;
      if (set.has(type)) set.delete(type);
      else set.add(type);
      persist(next);
      return next;
    });
  }, [persist]);

  const setLevel = useCallback((level: "l1" | "l2" | "l3", types: ReadonlySet<EventType>) => {
    setState((prev) => {
      const next: EventFilterState = {
        l1: new Set(prev.l1),
        l2: new Set(prev.l2),
        l3: new Set(prev.l3),
      };
      next[level] = new Set(types);
      persist(next);
      return next;
    });
  }, [persist]);

  const value = useMemo(() => ({ state, toggle, setLevel }), [state, toggle, setLevel]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useEventFilter(): EventFilterContextValue {
  const ctx = useContext(Ctx);
  if (!ctx) return { state: defaultFilterState(), toggle: () => {}, setLevel: () => {} };
  return ctx;
}
