"use client";

import { useEffect, useState } from "react";
import { fetchDialogActivity } from "@/lib/specDialogApi";
import type { SpecDialogActivityPush } from "@/types/spec-dialog";
import type { RawStep } from "./condenseSteps";

// 2026-11-12-92e9c: a turn's steps for its message. A turn this page watched keeps the steps pushed while it ran; a
// past turn reads every page of its run once — the condensed lines are few, so all of them are shown inline. Moved
// here from TurnActivityLine, which no longer fetches.

const PAGE = 500;

export interface TurnSteps {
  steps: RawStep[];
  firstAt: string | null;
  lastAt: string | null;
}

const NONE: TurnSteps = { steps: [], firstAt: null, lastAt: null };

export function useTurnSteps(runId?: string | null, live?: SpecDialogActivityPush[]): TurnSteps {
  const [fetched, setFetched] = useState<TurnSteps>(NONE);
  const watched = live !== undefined;
  useEffect(() => {
    if (watched || !runId) return;
    const abort = new AbortController();
    void readAll(runId, abort.signal).then(setFetched, () => undefined);
    return () => abort.abort();
  }, [runId, watched]);
  if (live) return { steps: live, firstAt: live[0]?.at ?? null, lastAt: live.at(-1)?.at ?? null };
  return fetched;
}

async function readAll(runId: string, signal: AbortSignal): Promise<TurnSteps> {
  let page = await fetchDialogActivity(runId, null, PAGE, signal);
  const steps: RawStep[] = [...page.steps];
  while (page.hasMore && page.nextSeq !== null) {
    page = await fetchDialogActivity(runId, page.nextSeq, PAGE, signal);
    steps.push(...page.steps);
  }
  return { steps, firstAt: page.firstAt ?? null, lastAt: page.lastAt ?? null };
}
