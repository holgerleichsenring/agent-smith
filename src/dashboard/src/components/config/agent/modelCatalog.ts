import type {
  AgentPricingEntry,
  ConfigCapabilities,
  ListedModelPrice,
  ModelRoleCapability,
  StudioAgent,
} from "@/lib/configApi";

// 2026-09-30-62bab: what the agent drawer derives from a draft — pure, so the tabs render
// and the tests assert one reading of it. The server stays the authority: it prices,
// resolves and refuses on save; these helpers only show the operator what it will do.

/** The operator's name for each role and what it covers. The raw key is shown beside it. */
export const ACTIVITY: Record<string, { label: string; covers: string }> = {
  primary: { label: "Coding", covers: "master loop, sub-agents, docs, gate retries" },
  planning: { label: "Planning", covers: "spec derivation, repository scope" },
  reasoning: {
    label: "Review & checks",
    covers: "diff review, premise check, spec cut, finding refutation",
  },
  contextGeneration: { label: "Project init", covers: "component discovery, context files" },
  codeMapGeneration: { label: "Code map", covers: "codebase map for analysis" },
  scout: { label: "Quick lookups", covers: "client surface, registry auth, bootstrap docs" },
  summarization: { label: "Summaries", covers: "knowledge compile, dialogue subjects" },
};

/** The display order when capabilities are unavailable — never guessed needs. */
const FALLBACK_ROLES: ModelRoleCapability[] = Object.keys(ACTIVITY).map((key) => ({
  key,
  optional: key !== "primary",
  needsStrong: false,
}));

export function activityLabel(role: string): string {
  return ACTIVITY[role]?.label ?? role;
}

export function roleList(capabilities: ConfigCapabilities | null): ModelRoleCapability[] {
  return capabilities?.roles ?? FALLBACK_ROLES;
}

/** The role an unset role follows — the backend's ModelRoleChain, one step. */
export function followsRole(role: string, models: Record<string, string>): string | null {
  if (role === "primary") return null;
  if (role === "codeMapGeneration" && models.scout) return "scout";
  return "primary";
}

/** The catalog entry a role resolves to, following inheritance; null when none does. */
export function effectiveEntry(role: string, models: Record<string, string>): string | null {
  let current: string | null = role;
  while (current) {
    const named = models[current];
    if (named) return named;
    current = followsRole(current, models);
  }
  return null;
}

/** Roles that name this entry themselves — inheritance excluded. */
export function rolesUsing(entry: string, models: Record<string, string>): string[] {
  return Object.entries(models)
    .filter(([, name]) => name === entry)
    .map(([role]) => role);
}

/** Roles that need a strong model and resolve to an entry the operator tiered fast. */
export function strongMismatches(draft: StudioAgent, capabilities: ConfigCapabilities | null): string[] {
  const catalog = draft.catalog ?? {};
  return roleList(capabilities)
    .filter((r) => r.needsStrong)
    .map((r) => r.key)
    .filter((key) => {
      const entry = effectiveEntry(key, draft.models);
      return entry != null && catalog[entry]?.tier === "fast";
    });
}

export type PriceSource = "override" | "list" | "none";

export interface ResolvedPrice {
  source: PriceSource;
  inputPerMillion: number | null;
  outputPerMillion: number | null;
  cacheReadPerMillion: number | null;
  /** The list entry that answered, when the list did. */
  listed: ListedModelPrice | null;
}

/** The override key for a model id — the server matches overrides on the exact id,
 *  ignoring case. */
export function overrideKey(
  pricing: StudioAgent["pricing"],
  model: string,
): string | null {
  const wanted = model.trim().toLowerCase();
  if (!wanted) return null;
  return Object.keys(pricing?.models ?? {}).find((k) => k.toLowerCase() === wanted) ?? null;
}

/** The list entry for a model id: exact id first, then an id whose name after its
 *  provider prefix is the model ("azure/gpt-4.1" answers "gpt-4.1"). */
export function findListed(list: ListedModelPrice[] | null, model: string): ListedModelPrice | null {
  const wanted = model.trim().toLowerCase();
  if (!wanted || !list) return null;
  const exact = list.find((m) => m.id.toLowerCase() === wanted);
  if (exact) return exact;
  return list.find((m) => bareName(m.id) === wanted) ?? null;
}

function bareName(id: string): string {
  const slash = id.lastIndexOf("/");
  return (slash >= 0 ? id.slice(slash + 1) : id).toLowerCase();
}

/** The price the resolver would charge for a model: override, else list, else nothing. */
export function resolvePrice(
  model: string,
  pricing: StudioAgent["pricing"],
  list: ListedModelPrice[] | null,
): ResolvedPrice {
  const listed = findListed(list, model);
  const key = overrideKey(pricing, model);
  if (key) {
    const o = pricing!.models[key];
    return {
      source: "override",
      inputPerMillion: o.inputPerMillion,
      outputPerMillion: o.outputPerMillion,
      cacheReadPerMillion: o.cacheReadPerMillion ?? null,
      listed,
    };
  }
  if (listed) {
    return {
      source: "list",
      inputPerMillion: listed.inputPerMillion,
      outputPerMillion: listed.outputPerMillion,
      cacheReadPerMillion: listed.cacheReadPerMillion,
      listed,
    };
  }
  return { source: "none", inputPerMillion: null, outputPerMillion: null, cacheReadPerMillion: null, listed: null };
}

export const SOURCE_LABEL: Record<PriceSource, string> = {
  override: "override",
  list: "price list",
  none: "not in list",
};

export function money(v: number | null | undefined): string {
  if (v == null) return "—";
  return `$${v < 0.1 && v > 0 ? v.toString() : v.toFixed(2)}`;
}

export function priceLine(p: ResolvedPrice): string {
  return `${money(p.inputPerMillion)} / ${money(p.outputPerMillion)} / ${money(p.cacheReadPerMillion)}`;
}

/** "$2.00 / $8.00" for a select option — cache read left to the card. */
export function shortPrice(p: ResolvedPrice): string {
  return p.source === "none" ? "no price" : `${money(p.inputPerMillion)} / ${money(p.outputPerMillion)}`;
}

export function windowLabel(tokens: number | null | undefined): string {
  if (!tokens) return "no window stated";
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(2).replace(/\.?0+$/, "")}M window`;
  if (tokens >= 1000) return `${Math.round(tokens / 1000)}k window`;
  return `${tokens} window`;
}

/** Overrides naming no model of the catalog — kept by the save, shown so they can go. */
export function orphanOverrides(draft: StudioAgent): string[] {
  const models = new Set(Object.values(draft.catalog ?? {}).map((e) => e.model.trim().toLowerCase()));
  return Object.keys(draft.pricing?.models ?? {}).filter((k) => !models.has(k.toLowerCase()));
}

/** Pricing with one model's override set or removed. Removing the last override sends an
 *  EMPTY table, which clears it — an absent table would keep what is stored. */
export function withOverride(
  pricing: StudioAgent["pricing"],
  model: string,
  entry: AgentPricingEntry | null,
): StudioAgent["pricing"] {
  const models = { ...(pricing?.models ?? {}) };
  const key = overrideKey(pricing, model);
  if (key) delete models[key];
  if (entry) models[model.trim()] = entry;
  if (!pricing && !entry) return pricing;
  return { models };
}
