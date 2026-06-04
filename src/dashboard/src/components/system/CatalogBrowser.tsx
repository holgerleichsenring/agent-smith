"use client";

import { useEffect, useState } from "react";
import { fetchCatalogContents, fetchSkillBody, type CatalogContents } from "@/lib/catalogApi";
import { CatalogBrowserView } from "./CatalogBrowserView";

// p0221: catalog browser container — the System "Skill catalog & vocabulary"
// page. Fetches the resolved catalog's contents once on mount; the view fetches
// each SKILL.md body lazily on expand.

export function CatalogBrowser() {
  const [contents, setContents] = useState<CatalogContents | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchCatalogContents(controller.signal)
      .then(setContents)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e.message);
      });
    return () => controller.abort();
  }, []);

  if (error) {
    return (
      <div className="content-shell dsh-body text-rose-700" data-testid="catalog-browser-error">
        Failed to load catalog: {error}
      </div>
    );
  }
  if (!contents) {
    return (
      <div className="content-shell dsh-body text-stone-400" data-testid="catalog-browser-loading">
        Loading catalog…
      </div>
    );
  }
  if (!contents.ready) {
    return (
      <div className="content-shell dsh-body text-stone-500" data-testid="catalog-browser-unready">
        Catalog not loaded yet — it binds when the first pipeline runs.
      </div>
    );
  }
  return <CatalogBrowserView contents={contents} loadBody={fetchSkillBody} />;
}
