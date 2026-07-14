"use client";

import { useCallback, useState } from "react";

// p0337: bulk "clear terminal runs" — one click empties finished/failed/
// cancelled runs, leaving running and queued untouched (the backend scopes it
// to terminal only, so it can never force-kill a live run). Two-click confirm
// like DeleteRunButton; the RunsChanged nudge refetches the list.
const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export function ClearTerminalRunsButton() {
  const [armed, setArmed] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onClick = useCallback(async () => {
    if (pending) return;
    if (!armed) { setArmed(true); return; }
    setPending(true);
    setError(null);
    try {
      const res = await fetch(`${API_BASE}/api/runs?state=terminal`, { method: "DELETE" });
      if (!res.ok) setError(`HTTP ${res.status}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "request failed");
    } finally {
      setArmed(false);
      setPending(false);
    }
  }, [armed, pending]);

  const label = pending ? "clearing…" : armed ? "confirm clear" : "clear finished";

  return (
    <button
      type="button"
      onClick={onClick}
      onMouseLeave={() => !pending && setArmed(false)}
      disabled={pending}
      data-testid="clear-terminal-runs"
      title={error ?? "Delete all finished, failed and cancelled runs"}
      className={`inline-flex flex-none items-center rounded px-2 py-0.5 text-xs font-medium border transition disabled:cursor-not-allowed disabled:opacity-60 ${
        armed
          ? "border-rose-300 bg-rose-50 text-rose-700"
          : "border-stone-200 bg-white text-stone-600 hover:border-rose-300 hover:bg-rose-50 hover:text-rose-700"
      }`}
    >
      {label}
    </button>
  );
}
