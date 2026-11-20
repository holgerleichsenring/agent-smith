"use client";

import { useEffect, useMemo, useState } from "react";
import { fetchRunSpec } from "@/lib/runSpecsApi";
import { fetchRunPasses } from "@/lib/ticketsApi";
import type { ProgressLedgerEntry } from "@/types/hub-events";
import type { RunPasses } from "@/types/tickets";
import { parseSpecRecord, type SpecStep } from "./specRecord";
import { stepLinesFromRecord, stepLinesFromLedger, type SpecStepLine } from "./specStepLines";

// 2026-11-19-1b72d: the steps of one spec of a run, with their state. The run's ledger answers for the spec it belongs
// to; any other spec is read once from its executed record and the run's recorded ledger transitions (specStepLines).
// A page that already holds the run's passes (the ticket page's store) hands them in, and they are not read again.

/** The run's passes as a caller already read them. */
export interface GivenPasses {
  data: RunPasses | null;
  loading: boolean;
  failed: boolean;
}

export interface SpecStepLinesRead {
  lines: SpecStepLine[];
  loading: boolean;
  /** The spec's record could not be read: there are no steps to show. */
  stepsFailed: boolean;
  /** The steps were read but the run's passes were not: every state is unknown, not open. */
  statesFailed: boolean;
}

export function useSpecStepLines(
  runId: string, specId: string | null, ledger: ProgressLedgerEntry[] | null, ledgerIsThisSpec: boolean, given?: GivenPasses,
): SpecStepLinesRead {
  const fromLedger = ledgerIsThisSpec && ledger !== null && ledger.length > 0;
  const read = !fromLedger && specId !== null;
  const ownPasses = given === undefined;
  const [steps, setSteps] = useState<SpecStep[] | null>(null);
  const [fetched, setFetched] = useState<RunPasses | null>(null);
  const [loading, setLoading] = useState(false);
  const [stepsFailed, setStepsFailed] = useState(false);
  const [ownStatesFailed, setOwnStatesFailed] = useState(false);

  useEffect(() => {
    if (!read || !specId) return;
    const ctrl = new AbortController();
    setLoading(true);
    setStepsFailed(false);
    setOwnStatesFailed(false);
    void (async () => {
      const [detail, passes] = await Promise.allSettled([
        fetchRunSpec(runId, specId, ctrl.signal),
        ownPasses ? fetchRunPasses(runId, ctrl.signal) : Promise.resolve(null),
      ]);
      if (ctrl.signal.aborted) return;
      if (detail.status === "fulfilled") {
        setSteps(detail.value?.record ? parseSpecRecord(detail.value.record)?.steps ?? [] : []);
      } else {
        setSteps(null);
        setStepsFailed(true);
      }
      setFetched(passes.status === "fulfilled" ? passes.value : null);
      setOwnStatesFailed(passes.status === "rejected");
      setLoading(false);
    })();
    return () => ctrl.abort();
  }, [runId, specId, read, ownPasses]);

  const passes = ownPasses ? fetched : given.data;
  const statesFailed = ownPasses ? ownStatesFailed : given.failed;
  const lines = useMemo(() => {
    if (fromLedger) return stepLinesFromLedger(ledger!);
    if (!steps) return [];
    const moved = (passes?.specs.find((s) => s.specId === specId)?.passes ?? [])
      .slice().sort((l, r) => l.ordinal - r.ordinal).flatMap((p) => p.stepsMoved);
    return stepLinesFromRecord(steps, moved);
  }, [fromLedger, ledger, steps, passes, specId]);
  if (fromLedger) return { lines, loading: false, stepsFailed: false, statesFailed: false };
  return { lines, loading: loading || (!ownPasses && given.loading), stepsFailed, statesFailed: statesFailed && steps !== null };
}
