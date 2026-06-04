"use client";

import { useCallback, useEffect, useMemo, useState } from "react";

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
  // p0227: while a run is live, follow the RUNNING step so the operator watches
  // it without clicking; on a finished run fall back to the failed step
  // (operators land to see what broke), then the last node.
  const running = items.find((i) => i.status === "run");
  const failed = items.find((i) => i.status === "fail");
  return (running ?? failed ?? items[items.length - 1]).id;
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
  // p0227: an empty sel means "follow the live step" — omit `n` so a refresh /
  // deep link stays in follow mode rather than pinning to a stale node.
  if (sel) params.set("n", sel);
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

  // p0227: NO adopt-once pinning. While the operator hasn't explicitly selected
  // (state.sel === null), `selected` is recomputed from defaultSelection every
  // render, so it FOLLOWS the running step as the pipeline advances — watch it
  // live without clicking. An explicit select() pins and stops following.
  const selected = state.sel ?? fallback;

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
      writeHash(prev.sel ?? "", exp); // keep follow mode when unpinned
      return { sel: prev.sel, exp };
    });
  }, []);

  // The selected (followed) node is always shown expanded so its body is open.
  const expanded = useMemo(() => {
    const e = new Set(state.exp);
    if (selected) e.add(selected);
    return e;
  }, [state.exp, selected]);

  return { selected, expanded, select, toggle };
}
