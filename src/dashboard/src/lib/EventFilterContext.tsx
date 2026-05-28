"use client";

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  EventFilterState,
  defaultFilterState,
  parseFilterFromQuery,
  writeFilterToQuery,
} from "./eventFilterQuery";
import {
  DimensionFilterState,
  DimensionKey,
  defaultDimensionState,
  parseDimensionsFromQuery,
  writeDimensionsToQuery,
} from "./dimensionFilterQuery";
import type { EventType } from "@/types/hub-events";

interface EventFilterContextValue {
  state: EventFilterState;
  toggle: (level: "l1" | "l2" | "l3", type: EventType) => void;
  setLevel: (level: "l1" | "l2" | "l3", types: ReadonlySet<EventType>) => void;
  /** p0173f: chip-style dimensions filter the trail by Agent / Sandbox / Pipeline / Activity. */
  dimensions: DimensionFilterState;
  toggleDimension: (key: DimensionKey, value: string) => void;
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
  const seedDimensionsFromUrl = useMemo(
    () => parseDimensionsFromQuery(new URLSearchParams(searchParams.toString())),
    [searchParams],
  );
  const [state, setState] = useState<EventFilterState>(seedFromUrl);
  const [dimensions, setDimensions] = useState<DimensionFilterState>(seedDimensionsFromUrl);

  const persist = useCallback((nextState: EventFilterState, nextDims: DimensionFilterState) => {
    let params = writeFilterToQuery(nextState, new URLSearchParams(searchParams.toString()));
    params = writeDimensionsToQuery(nextDims, params);
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
      persist(next, dimensions);
      return next;
    });
  }, [persist, dimensions]);

  const setLevel = useCallback((level: "l1" | "l2" | "l3", types: ReadonlySet<EventType>) => {
    setState((prev) => {
      const next: EventFilterState = {
        l1: new Set(prev.l1),
        l2: new Set(prev.l2),
        l3: new Set(prev.l3),
      };
      next[level] = new Set(types);
      persist(next, dimensions);
      return next;
    });
  }, [persist, dimensions]);

  const toggleDimension = useCallback((key: DimensionKey, value: string) => {
    setDimensions((prev) => {
      const next: DimensionFilterState = {
        agent: new Set(prev.agent),
        sandbox: new Set(prev.sandbox),
        pipeline: new Set(prev.pipeline),
        activity: new Set(prev.activity),
      };
      const set = next[key] as Set<string>;
      if (set.has(value)) set.delete(value);
      else set.add(value);
      persist(state, next);
      return next;
    });
  }, [persist, state]);

  const value = useMemo(
    () => ({ state, toggle, setLevel, dimensions, toggleDimension }),
    [state, toggle, setLevel, dimensions, toggleDimension],
  );
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useEventFilter(): EventFilterContextValue {
  const ctx = useContext(Ctx);
  if (!ctx) return {
    state: defaultFilterState(),
    toggle: () => {},
    setLevel: () => {},
    dimensions: defaultDimensionState(),
    toggleDimension: () => {},
  };
  return ctx;
}
