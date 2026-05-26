// Thin server-side fetch helpers for the Job-Viewer. All calls go to the
// backend (set via AGENT_SMITH_API_URL env on the server); they are invoked
// from Server Components, so credentials/CORS aren't a concern here.

export interface RunMeta {
  runId: string;
  pipelineName: string;
  status: string;
  startedAt: string | null;
  durationSeconds: number;
  repoMode: string;
  sandboxCount: number;
  repos: string[];
  ticket: string | null;
  type: string | null;
}

export interface RunArtefact {
  filename: string;
  sizeBytes: number;
  contentType: string;
}

export interface JobListResponse {
  jobs: RunMeta[];
  total: number;
  page: number;
  pageSize: number;
}

export interface JobDetailResponse {
  meta: RunMeta;
  artefacts: RunArtefact[];
}

function apiBase(): string {
  return process.env.AGENT_SMITH_API_URL ?? "http://localhost:8081";
}

export async function listJobs(): Promise<JobListResponse> {
  const res = await fetch(`${apiBase()}/api/jobs`, { cache: "no-store" });
  if (!res.ok) {
    return { jobs: [], total: 0, page: 1, pageSize: 50 };
  }
  return (await res.json()) as JobListResponse;
}

export async function getJob(id: string): Promise<JobDetailResponse | null> {
  const res = await fetch(`${apiBase()}/api/jobs/${encodeURIComponent(id)}`, { cache: "no-store" });
  if (!res.ok) return null;
  return (await res.json()) as JobDetailResponse;
}

export async function getJobFile(id: string, filename: string): Promise<string | null> {
  const res = await fetch(
    `${apiBase()}/api/jobs/${encodeURIComponent(id)}/files/${encodeURIComponent(filename)}`,
    { cache: "no-store" },
  );
  if (!res.ok) return null;
  return await res.text();
}
