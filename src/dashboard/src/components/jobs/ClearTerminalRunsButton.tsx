"use client";

import { useCallback, useState } from "react";
import { apiFetch } from "@/lib/apiResponse";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";

// p0337: bulk "clear finished" — one click empties finished/failed/cancelled
// runs, leaving running and queued untouched (the backend scopes it to terminal
// only, so it can never force-kill a live run). Two-click confirm like
// DeleteRunButton, and the armed state says what goes: the runs AND the verdicts
// operators recorded on their criteria. The RunsChanged nudge refetches the list.
// The endpoint needs runs.delete; a signed-in caller the server resolved without
// it is not offered the button at all.
const RUNS_DELETE = "runs.delete";

export function ClearTerminalRunsButton() {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  const [armed, setArmed] = useState(false);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onClick = useCallback(async () => {
    if (pending) return;
    if (!armed) { setArmed(true); return; }
    setPending(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/runs?state=terminal`, { method: "DELETE" });
      if (!res.ok) setError(`HTTP ${res.status}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "request failed");
    } finally {
      setArmed(false);
      setPending(false);
    }
  }, [armed, pending]);

  if (identity && !identity.permissions.includes(RUNS_DELETE)) return null;

  const label = pending
    ? "clearing…"
    : armed
      ? "confirm: finished runs and their verdict history go"
      : "clear finished";

  return (
    <button
      type="button"
      onClick={onClick}
      onMouseLeave={() => !pending && setArmed(false)}
      disabled={pending}
      data-testid="clear-terminal-runs"
      title={error ?? "Delete every finished, failed and cancelled run, with the criterion verdicts recorded on it"}
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
