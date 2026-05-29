// p0173f: URL-string round-trip for the FilterRail's four dimension
// chip groups (agent, sandbox, pipeline, activity). Defaults to empty
// for every dimension (no filtering). Each dimension has its own URL
// param; entries are comma-separated, percent-encoded by URLSearchParams.

export type DimensionKey = "agent" | "sandbox" | "pipeline" | "activity";

export interface DimensionFilterState {
  agent: ReadonlySet<string>;
  sandbox: ReadonlySet<string>;
  pipeline: ReadonlySet<string>;
  activity: ReadonlySet<string>;
}

export function defaultDimensionState(): DimensionFilterState {
  return {
    agent: new Set(),
    sandbox: new Set(),
    pipeline: new Set(),
    activity: new Set(),
  };
}

const PARAM_PREFIX = "d.";

export function parseDimensionsFromQuery(params: URLSearchParams): DimensionFilterState {
  return {
    agent: parseList(params.get(`${PARAM_PREFIX}agent`)),
    sandbox: parseList(params.get(`${PARAM_PREFIX}sandbox`)),
    pipeline: parseList(params.get(`${PARAM_PREFIX}pipeline`)),
    activity: parseList(params.get(`${PARAM_PREFIX}activity`)),
  };
}

export function writeDimensionsToQuery(
  state: DimensionFilterState, base: URLSearchParams,
): URLSearchParams {
  const params = new URLSearchParams(base);
  serialise("agent", state.agent, params);
  serialise("sandbox", state.sandbox, params);
  serialise("pipeline", state.pipeline, params);
  serialise("activity", state.activity, params);
  return params;
}

function parseList(raw: string | null): ReadonlySet<string> {
  if (raw === null || raw.trim() === "") return new Set();
  const result = new Set<string>();
  for (const token of raw.split(",")) {
    const trimmed = token.trim();
    if (trimmed.length > 0) result.add(trimmed);
  }
  return result;
}

function serialise(
  dimension: DimensionKey,
  current: ReadonlySet<string>,
  params: URLSearchParams,
): void {
  const key = `${PARAM_PREFIX}${dimension}`;
  if (current.size === 0) {
    params.delete(key);
    return;
  }
  const ordered = [...current].sort();
  params.set(key, ordered.join(","));
}
