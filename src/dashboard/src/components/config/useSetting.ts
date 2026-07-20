"use client";

import { useCallback, useEffect, useState } from "react";
import type { SettingKey, SettingShapes } from "@/lib/configApi";
import { fetchSetting, saveSetting } from "@/lib/configApi";

// p0353: loads one settings singleton and owns its save. Mirrors useConfigCatalog's
// load/reload shape but for a single typed doc: a save PUTs the value, then re-reads
// the persisted result so the form shows exactly what the backend stored (and the
// live-applied epoch bump has already fired server-side). One form per settings key,
// so there is no cross-view shared state to lift into a provider — each form reloads
// itself.

export interface UseSetting<K extends SettingKey> {
  value: SettingShapes[K] | null;
  loading: boolean;
  error: string | null;
  saving: boolean;
  saveError: string | null;
  reload: () => Promise<void>;
  save: (value: SettingShapes[K]) => Promise<void>;
}

export function useSetting<K extends SettingKey>(key: K): UseSetting<K> {
  const [value, setValue] = useState<SettingShapes[K] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const load = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        setValue(await fetchSetting(key, signal));
      } catch (err) {
        if ((err as Error).name === "AbortError") return;
        setError((err as Error).message);
      } finally {
        setLoading(false);
      }
    },
    [key],
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const reload = useCallback(() => load(), [load]);

  const save = useCallback(
    async (next: SettingShapes[K]) => {
      setSaving(true);
      setSaveError(null);
      try {
        setValue(await saveSetting(key, next));
      } catch (err) {
        setSaveError((err as Error).message);
        throw err;
      } finally {
        setSaving(false);
      }
    },
    [key],
  );

  return { value, loading, error, saving, saveError, reload, save };
}
