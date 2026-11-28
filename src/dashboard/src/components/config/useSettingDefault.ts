"use client";

import { useEffect, useState } from "react";
import type { SettingKey, SettingShapes } from "@/lib/configApi";
import { fetchSettingDefault } from "@/lib/configApi";

// 2026-11-28-9bd1: the default of one settings singleton, read once per section. A server that
// cannot answer (an older one, a refusal) leaves it null and settled — the section then shows no
// count rather than guessing one.

export interface UseSettingDefault<K extends SettingKey> {
  value: SettingShapes[K] | null;
  settled: boolean;
}

export function useSettingDefault<K extends SettingKey>(key: K): UseSettingDefault<K> {
  const [state, setState] = useState<UseSettingDefault<K>>({ value: null, settled: false });

  useEffect(() => {
    const controller = new AbortController();
    setState({ value: null, settled: false });
    fetchSettingDefault(key, controller.signal)
      .then((value) => setState({ value, settled: true }))
      .catch((err: Error) => {
        if (err?.name !== "AbortError") setState({ value: null, settled: true });
      });
    return () => controller.abort();
  }, [key]);

  return state;
}
