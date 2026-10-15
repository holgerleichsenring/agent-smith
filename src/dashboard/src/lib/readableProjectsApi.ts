import { getJson } from "@/lib/apiResponse";

// 2026-10-15-4a4de: GET /api/projects — the configured project names this caller may read (runs.read), a bare JSON
// array sorted case-insensitively. Not configApi's projectsApi, which is the Config Studio's config.read client.

export async function fetchReadableProjects(signal?: AbortSignal): Promise<string[]> {
  return getJson<string[]>("/api/projects", signal);
}
