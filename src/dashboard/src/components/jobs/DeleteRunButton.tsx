"use client";

import { useCallback, useState } from "react";

// p0337: delete a run (any state). Two-click confirm — no modal primitive
// exists in the dashboard, so the button arms on the first click ("delete?")
// and fires on the second. A successful delete needs no local list mutation:
// the backend fires a RunsChanged nudge and every dashboard drops the run on
// its next refetch. onDeleted lets the detail view navigate away (the run it
// showed is gone).
interface Props {
  runId: string;
  onDeleted?: () => void;
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export function DeleteRunButton({ runId, onDeleted }: Props) {
  const [armed, setArmed] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onClick = useCallback(async (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (pending) return;
    if (!armed) { setArmed(true); return; }
    setPending(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}`, { method: "DELETE" });
      if (res.ok || res.status === 204) { onDeleted?.(); return; }
      setError(`HTTP ${res.status}`);
      setArmed(false);
      setPending(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "request failed");
      setArmed(false);
      setPending(false);
    }
  }, [armed, pending, runId, onDeleted]);

  const label = pending ? "deleting…" : armed ? "confirm delete" : "delete";
  const armedTone = armed
    ? "border-rose-300 bg-rose-50 text-rose-700"
    : "border-stone-200 bg-white text-stone-700 hover:border-rose-300 hover:bg-rose-50 hover:text-rose-700";

  return (
    <button
      type="button"
      onClick={onClick}
      onMouseLeave={() => !pending && setArmed(false)}
      disabled={pending}
      data-testid={`delete-run-${runId}`}
      title={error ?? "Delete this run and everything it left behind"}
      className={`inline-flex flex-none items-center rounded px-2 py-0.5 text-xs font-medium border transition disabled:cursor-not-allowed disabled:opacity-60 ${armedTone}`}
    >
      {label}
    </button>
  );
}
