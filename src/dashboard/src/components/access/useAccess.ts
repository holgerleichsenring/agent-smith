"use client";

import { useCallback, useEffect, useState } from "react";
import type { AccessDocument, AccessView } from "@/lib/accessApi";
import { fetchAccess, forgetPerson, saveAccess } from "@/lib/accessApi";

// 2026-08-26-7a51: loads the access surface and owns its two writes — the whole-document
// save, and the one action that removes a person. Mirrors useSetting's load/save shape,
// because a settings save is what this is underneath: attributed, versioned, revertible,
// and applied to the next request.
//
// The loader holds the THROWN value rather than its message: a refusal is a state the pane
// renders, not a sentence.

export interface UseAccess {
  view: AccessView | null;
  loading: boolean;
  error: Error | null;
  saving: boolean;
  saveError: Error | null;
  save: (document: AccessDocument) => Promise<void>;
  forget: (id: string) => Promise<void>;
}

export function useAccess(): UseAccess {
  const [view, setView] = useState<AccessView | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<Error | null>(null);

  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      setView(await fetchAccess(signal));
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      setError(err as Error);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  const write = useCallback(async (send: () => Promise<AccessView>) => {
    setSaving(true);
    setSaveError(null);
    try {
      setView(await send());
    } catch (err) {
      setSaveError(err as Error);
    } finally {
      setSaving(false);
    }
  }, []);

  return {
    view,
    loading,
    error,
    saving,
    saveError,
    save: useCallback((document: AccessDocument) => write(() => saveAccess(document)), [write]),
    forget: useCallback((id: string) => write(() => forgetPerson(id)), [write]),
  };
}
