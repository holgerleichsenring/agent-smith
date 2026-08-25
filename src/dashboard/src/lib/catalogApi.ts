// p0221: client for the lazy catalog-contents endpoints. The list is cheap
// (names + descriptions); each SKILL.md body is fetched on demand when a card
// is expanded, so the per-run event stream is never bloated with bodies.

import { apiFetch, getJson, readJson } from "@/lib/apiResponse";

export interface CatalogEntry {
  name: string;
  role: string;
  description: string;
}

export interface CatalogConcept {
  name: string;
  type: string;
  description: string;
}

export interface CatalogContents {
  ready: boolean;
  masters: CatalogEntry[];
  skills: CatalogEntry[];
  concepts: CatalogConcept[];
}

export async function fetchCatalogContents(signal?: AbortSignal): Promise<CatalogContents> {
  return getJson<CatalogContents>(`/api/catalog`, signal);
}

export async function fetchSkillBody(name: string, signal?: AbortSignal): Promise<string | null> {
  const path = `/api/catalog/skills/${encodeURIComponent(name)}`;
  const res = await apiFetch(path, { signal });
  if (res.status === 404) return null;
  const body = await readJson<{ markdown?: string }>(res, path);
  return body.markdown ?? null;
}
