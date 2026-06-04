// p0221: client for the lazy catalog-contents endpoints. The list is cheap
// (names + descriptions); each SKILL.md body is fetched on demand when a card
// is expanded, so the per-run event stream is never bloated with bodies.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

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
  const res = await fetch(`${API_BASE}/api/catalog`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as CatalogContents;
}

export async function fetchSkillBody(name: string, signal?: AbortSignal): Promise<string | null> {
  const res = await fetch(`${API_BASE}/api/catalog/skills/${encodeURIComponent(name)}`, { signal });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = (await res.json()) as { name: string; markdown: string };
  return body.markdown;
}
