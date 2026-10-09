"use client";

import { useEffect, useState } from "react";

// 2026-10-09-86e1: bytes the page shows, fetched with the bearer token and drawn from an object
// URL. An <img src> to the API carries no Authorization header, so with sign-in on every stored
// image rendered nothing. The URL is revoked when the source changes or the component goes.

export type BlobUrlState = { url: string | null; failed: boolean };

export function useBlobUrl(load: (() => Promise<Blob>) | null, key: string | null): BlobUrlState {
  const [state, setState] = useState<BlobUrlState>({ url: null, failed: false });
  useEffect(() => {
    if (!load || key === null) return;
    let live = true;
    let made: string | null = null;
    setState({ url: null, failed: false });
    load()
      .then((blob) => {
        if (!live) return;
        made = URL.createObjectURL(blob);
        setState({ url: made, failed: false });
      })
      .catch(() => live && setState({ url: null, failed: true }));
    return () => {
      live = false;
      if (made) URL.revokeObjectURL(made);
    };
    // The key names the bytes; `load` is a fresh closure on every render of its caller.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  return state;
}
