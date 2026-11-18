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
