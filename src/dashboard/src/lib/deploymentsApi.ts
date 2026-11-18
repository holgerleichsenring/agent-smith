// 2026-11-18-3863c/d: a project's deployment bindings (runs.read) and starting a deploy (runs.control). A refusal the
// server wrote as a sentence (an unbound environment, a version no manifest may carry) is shown as is.

import { apiFetch, getJson, refused } from "@/lib/apiResponse";

export interface DeploymentEnvironmentView {
  name: string;
  approval: boolean;
  repo?: string | null;
}

export interface DeploymentBindingView {
  context: string;
  contextRepo?: string | null;
  template: string;
  environments: DeploymentEnvironmentView[];
}

export interface DeployStart {
  context: string;
  contextRepo?: string | null;
  environment: string;
  version?: string | null;
  ticketId?: string | null;
}

export type DeployAnswer = { kind: "started"; runId: string } | { kind: "refused"; text: string };

const path = (project: string) => `/api/projects/${encodeURIComponent(project)}/deployments`;

export function listDeployments(project: string, signal?: AbortSignal): Promise<DeploymentBindingView[]> {
  return getJson<DeploymentBindingView[]>(path(project), signal);
}

export async function startDeploy(project: string, start: DeployStart): Promise<DeployAnswer> {
  const res = await apiFetch(path(project), {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(start),
  });
  if (res.ok) return { kind: "started", runId: ((await res.json()) as { runId: string }).runId };
  if (res.status === 400 || res.status === 404 || res.status === 409) return { kind: "refused", text: (await res.text()).trim() };
  throw await refused(res, path(project));
}

/** 2026-11-18-3863f: one failed check, job or task, as the CI reader and the pipeline runner tell it. */
export interface DeployFailure {
  name: string;
  url?: string | null;
  excerpt: string;
}

/** 2026-11-18-3863f: where a deploy run's delivery stands, followed by the server until it settles. */
export interface DeployRecordView {
  runId: string;
  project: string;
  context: string;
  environment: string;
  version: string;
  delivery: string;
  repo: string;
  url: string;
  pullRequestState?: string | null;
  ciState: string;
  failures: DeployFailure[];
  deliveredAt: string;
  checkedAt?: string | null;
  settledAt?: string | null;
  note?: string | null;
}

/** The run's deploy record, or null for a run that delivered nothing (refused, or already at the version). */
export async function getDeployRecord(runId: string, signal?: AbortSignal): Promise<DeployRecordView | null> {
  const at = `/api/runs/${encodeURIComponent(runId)}/deployment`;
  const res = await apiFetch(at, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw await refused(res, at);
  return (await res.json()) as DeployRecordView;
}
