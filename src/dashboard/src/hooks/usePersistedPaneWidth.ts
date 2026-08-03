"use client";

import { useCallback, useEffect, useState } from "react";

// p0395: one persisted pane dimension for the resizable trace drawer. Null means
// "the stylesheet default" — nothing is written until the operator actually
// drags. The stored value is read in an effect (not the initial state) so the
// server-rendered markup and the first client render agree; the drawer is
// closed at that moment, so the late application is invisible.

export function usePersistedPaneWidth(
  storageKey: string,
): [number | null, (width: number) => void] {
  const [width, setWidth] = useState<number | null>(null);

  useEffect(() => {
    setWidth(read(storageKey));
  }, [storageKey]);

  const set = useCallback(
    (value: number) => {
      const rounded = Math.round(value);
      setWidth(rounded);
      try {
        window.localStorage.setItem(storageKey, String(rounded));
      } catch {
        /* storage unavailable — the size still applies for this session */
      }
    },
    [storageKey],
  );

  return [width, set];
}

function read(key: string): number | null {
  try {
    const parsed = Number.parseInt(window.localStorage.getItem(key) ?? "", 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  } catch {
    return null;
  }
}
