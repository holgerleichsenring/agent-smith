import type { ListedModelPrice } from "@/lib/configApi";

// 2026-10-12-6bfae: the picker offered the first eight substring hits in list order, so for
// "sonnet-5-5" an Anthropic agent saw eight Bedrock/Azure/Vertex spellings and never the id
// its API accepts. Matches are ranked instead: an id from a provider the agent's API takes as
// written first, then where the query matches — at the start, at a word boundary, anywhere —
// then the shorter id. Ranking, not filtering: an OpenAI-compatible endpoint may run any id.

const BOUNDARY = /[-/.:@_]/;

function matchQuality(id: string, query: string): number {
  if (id.startsWith(query)) return 0;
  let at = id.indexOf(query);
  while (at > 0) {
    if (BOUNDARY.test(id[at - 1])) return 1;
    at = id.indexOf(query, at + 1);
  }
  return 2;
}

/** The list entries whose id contains the query, best first, at most `max` of them. The exact
 *  id is left out: it is already what the field holds. */
export function rankModelMatches(
  list: ListedModelPrice[],
  query: string,
  preferredProviders: readonly string[],
  max: number,
): ListedModelPrice[] {
  const q = query.trim().toLowerCase();
  const preferred = new Set(preferredProviders);
  return list
    .map((m) => ({ m, id: m.id.toLowerCase() }))
    .filter(({ id }) => id.includes(q) && id !== q)
    .map(({ m, id }) => ({ m, id, fit: preferred.has(m.provider) ? 0 : 1, quality: matchQuality(id, q) }))
    .sort((a, b) => a.fit - b.fit || a.quality - b.quality || a.id.length - b.id.length || a.id.localeCompare(b.id))
    .slice(0, max)
    .map(({ m }) => m);
}

/** The price-list providers whose ids the given agent type's API accepts as written; the
 *  server keys them by lower-case type name. Empty when the server names none. */
export function preferredProvidersFor(
  priceLists: Record<string, string[]> | undefined,
  providerType: string | null | undefined,
): string[] {
  if (!priceLists || !providerType) return [];
  return priceLists[providerType.trim().toLowerCase()] ?? [];
}
