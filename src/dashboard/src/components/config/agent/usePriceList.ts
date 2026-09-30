"use client";

import { useEffect, useState } from "react";
import { fetchModelPrices, type ModelPriceList } from "@/lib/configApi";

// 2026-09-30-62bab: the bundled public price list (~3300 models), read once per page and
// cached module-wide like the capabilities descriptor. `null` while loading or when the
// list could not be read — the drawer then says so rather than calling a model unpriced.

let cached: ModelPriceList | null = null;
let inflight: Promise<ModelPriceList> | null = null;

export interface UsePriceList {
  list: ModelPriceList | null;
  failed: boolean;
}

export function usePriceList(): UsePriceList {
  const [list, setList] = useState<ModelPriceList | null>(cached);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (cached) return;
    let alive = true;
    // Deferred so a missing or throwing fetcher lands in .catch rather than in React.
    inflight ??= Promise.resolve().then(() => fetchModelPrices());
    inflight
      .then((prices) => {
        cached = prices;
        if (alive) setList(prices);
      })
      .catch(() => {
        inflight = null;
        if (alive) setFailed(true);
      });
    return () => {
      alive = false;
    };
  }, []);

  return { list, failed };
}

/** Test hook: drop the module cache so each test starts cold. */
export function resetPriceListCache(): void {
  cached = null;
  inflight = null;
}
