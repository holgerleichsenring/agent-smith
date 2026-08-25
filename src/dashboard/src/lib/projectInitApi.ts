// p0489: the dashboard's WRITE client for starting an initialization. Init is the
// only pipeline an operator triggers by hand here, so the endpoint takes no
// pipeline parameter. No ticket is created, read or transitioned on this path —
// init-project is ticketless by design.
//
// The refusals are part of the contract, not exceptions: 409 answers with the run
// id of the init that is ALREADY going (pressing again opens that run instead of
// starting a second), 503 with the reason the run does not fit right now, and 4xx
// when no such project is configured.

import { apiFetch } from "@/lib/apiResponse";

export type InitLaunchOutcome = "started" | "already-running" | "refused";

export interface InitLaunch {
  outcome: InitLaunchOutcome;
  runId: string | null;
  reason: string | null;
}

interface InitLaunchBody {
  runId?: string | null;
  reason?: string | null;
}

// p0490: what the operator ticked on THIS launch travels with it. Auto-accept is not
// project configuration — consent belongs to the click that started this run — so it is
// stated explicitly on every request rather than remembered anywhere.
export interface InitOptions {
  autoCompletePullRequests: boolean;
}

export async function startProjectInit(
  project: string,
  options: InitOptions,
  signal?: AbortSignal,
): Promise<InitLaunch> {
  const res = await apiFetch(`/api/projects/${encodeURIComponent(project)}/init`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(options),
    signal,
  });
  const body = (await readBody(res)) ?? {};
  if (res.ok) return { outcome: "started", runId: body.runId ?? null, reason: null };
  if (res.status === 409) {
    return {
      outcome: "already-running",
      runId: body.runId ?? null,
      reason: body.reason ?? "An initialization is already running.",
    };
  }
  return {
    outcome: "refused",
    runId: null,
    reason: body.reason ?? `Could not start the initialization (HTTP ${res.status}).`,
  };
}

// A refusal body is the reason the button renders; a body that is not JSON must
// not swallow the status the reason is derived from.
async function readBody(res: Response): Promise<InitLaunchBody | null> {
  try {
    return (await res.json()) as InitLaunchBody;
  } catch {
    return null;
  }
}
