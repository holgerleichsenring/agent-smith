"use client";

import { useCallback, useEffect, useState } from "react";

// 2026-11-17-6d9ec: the open step is part of the address — ?step=24 — so a reload or a shared link lands on it. The
// graph view's links were #n=step-24 (with x=… for its expanded rows); such a hash is read once and rewritten to
// ?step=24. Read after mount through the History API, like the runs filter.

const PARAM = "step";
const OLD_HASH = /(?:^|&)n=step-(\d+)(?:&|$)/;

/** The step an address names: ?step=, else the graph view's #n=step-. */
export function stepFromAddress(search: string, hash: string): number | null {
  const fromQuery = new URLSearchParams(search).get(PARAM);
  if (fromQuery !== null && /^\d+$/.test(fromQuery)) return Number(fromQuery);
  const old = OLD_HASH.exec(hash.replace(/^#/, ""));
  return old ? Number(old[1]) : null;
}

function write(step: number | null): void {
  const url = new URL(window.location.href);
  if (step === null) url.searchParams.delete(PARAM);
  else url.searchParams.set(PARAM, String(step));
  const hash = OLD_HASH.test(url.hash.replace(/^#/, "")) ? "" : url.hash;
  window.history.replaceState(null, "", `${url.pathname}${url.search}${hash}`);
}

export function useStepAddress(): [number | null, (step: number | null) => void] {
  const [step, setStep] = useState<number | null>(null);
  useEffect(() => {
    const read = () => {
      const found = stepFromAddress(window.location.search, window.location.hash);
      setStep(found);
      if (found !== null && !new URLSearchParams(window.location.search).has(PARAM)) write(found);
    };
    read();
    window.addEventListener("popstate", read);
    return () => window.removeEventListener("popstate", read);
  }, []);
  const set = useCallback((next: number | null) => {
    write(next);
    setStep(next);
  }, []);
  return [step, set];
}
