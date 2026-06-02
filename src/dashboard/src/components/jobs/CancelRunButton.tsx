"use client";

import { useCallback, useState } from "react";

// p0200: per-Active-card cancel control. Optimistic UI flips the button
// label to "cancelling…" the moment the operator clicks; the backend
// confirms via the RunCancelRequestedEvent on the run stream, which
// flips snapshot.cancelRequested for every open tab. Idempotent: once
// the local cancelling flag (or the snapshot flag) is set, re-clicks
// no-op.
interface Props {
  runId: string;
  cancelRequested: boolean;
}

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export function CancelRunButton({ runId, cancelRequested }: Props) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const disabled = pending || cancelRequested;
  const label = disabled ? "cancelling…" : "cancel";

  const onClick = useCallback(async (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (disabled) return;
    setPending(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/runs/${encodeURIComponent(runId)}/cancel`, {
        method: "POST",
      });
      if (!res.ok && res.status !== 202) setError(`HTTP ${res.status}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "request failed");
      setPending(false);
    }
  }, [disabled, runId]);

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      data-testid={`cancel-run-${runId}`}
      title={error ?? "Cancel this run"}
      className="inline-flex flex-none items-center rounded px-2 py-0.5 text-xs font-medium border border-stone-200 bg-white text-stone-700 transition hover:border-rose-300 hover:bg-rose-50 hover:text-rose-700 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:border-stone-200 disabled:hover:bg-white disabled:hover:text-stone-700"
    >
      {label}
    </button>
  );
}
