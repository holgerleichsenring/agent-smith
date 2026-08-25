"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import {
  DEFAULT_RUNTIME_SETTINGS,
  loadRuntimeSettings,
  type RuntimeSettings,
} from "./runtimeSettings";

// 2026-08-25-21ae: the settings document has ONE reader, above the route
// boundary, beside the event store and the config catalog. Every consumer
// resolving it for itself would give the application a window in which one
// component believes a setting is on and another does not.
//
// No provider = every setting at its default, the same answer an absent document
// gives. A surface rendered outside the shell is honest about being unconfigured
// rather than throwing.

const Ctx = createContext<RuntimeSettings>(DEFAULT_RUNTIME_SETTINGS);

export function RuntimeSettingsProvider({
  children,
  settings,
}: {
  children: ReactNode;
  settings?: RuntimeSettings;
}) {
  const [resolved, setResolved] = useState<RuntimeSettings>(settings ?? DEFAULT_RUNTIME_SETTINGS);

  // Fetched after mount, never during render: the document lives on the
  // browser's origin, and a relative URL has no meaning while the shell is being
  // prerendered on the server.
  useEffect(() => {
    if (settings) return;
    let live = true;
    void loadRuntimeSettings().then((value) => {
      if (live) setResolved(value);
    });
    return () => {
      live = false;
    };
  }, [settings]);

  return <Ctx.Provider value={settings ?? resolved}>{children}</Ctx.Provider>;
}

/** The settings this installation was started with. */
export function useRuntimeSettings(): RuntimeSettings {
  return useContext(Ctx);
}
