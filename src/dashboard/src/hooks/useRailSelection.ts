"use client";

import { useCallback, useEffect, useState } from "react";

// p0205: selection + expansion state for the two-pane run detail, mirrored into
// the URL hash (#n=<selected>&x=<comma-separated expanded ids>) so a deep link
// and a refresh restore exactly what the operator was looking at. Default with
// no hash: the first failed step (operators land on a run to see what broke),
// falling back to the last node when nothing failed.

export interface RailSelectable {
  id: string;
  status?: string;
}

export interface RailSelection {
  selected: string;
  expanded: Set<string>;
  select: (id: string, parentId?: string) => void;
  toggle: (id: string) => void;
}

export function defaultSelection(items: readonly RailSelectable[]): string {
  if (items.length === 0) return "";
  const failed = items.find((i) => i.status === "fail");
  return (failed ?? items[items.length - 1]).id;
}

interface HashState {
  sel: string | null;
  exp: Set<string>;
}

function readHash(): HashState {
  if (typeof window === "undefined") return { sel: null, exp: new Set() };
  const params = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  const exp = new Set((params.get("x") ?? "").split(",").filter(Boolean));
  return { sel: params.get("n"), exp };
}

function writeHash(sel: string, exp: Set<string>): void {
  if (typeof window === "undefined") return;
  const params = new URLSearchParams();
  params.set("n", sel);
  if (exp.size > 0) params.set("x", [...exp].join(","));
  const { pathname, search } = window.location;
  window.history.replaceState(null, "", `${pathname}${search}#${params.toString()}`);
}

export function useRailSelection(items: readonly RailSelectable[]): RailSelection {
  const fallback = defaultSelection(items);
  const [state, setState] = useState<HashState>(() => {
    const h = readHash();
    return { sel: h.sel, exp: h.exp };
  });

  // Adopt the computed fallback once nodes arrive (events stream in async) and
  // no explicit hash selection exists yet.
  useEffect(() => {
    if (state.sel === null && fallback) {
      const initialExp = new Set(state.exp);
      initialExp.add(fallback);
      setState({ sel: fallback, exp: initialExp });
      writeHash(fallback, initialExp);
    }
  }, [fallback, state.sel, state.exp]);

  useEffect(() => {
    const onHashChange = () => setState(readHash());
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  const select = useCallback((id: string, parentId?: string) => {
    setState((prev) => {
      const exp = new Set(prev.exp);
      if (parentId) exp.add(parentId);
      writeHash(id, exp);
      return { sel: id, exp };
    });
  }, []);

  const toggle = useCallback((id: string) => {
    setState((prev) => {
      const exp = new Set(prev.exp);
      if (exp.has(id)) exp.delete(id);
      else exp.add(id);
      writeHash(prev.sel ?? id, exp);
      return { sel: prev.sel, exp };
    });
  }, []);

  return { selected: state.sel ?? fallback, expanded: state.exp, select, toggle };
}
