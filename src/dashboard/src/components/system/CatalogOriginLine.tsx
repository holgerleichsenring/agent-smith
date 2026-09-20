import type { CatalogOrigin } from "@/lib/catalogApi";
import { CatalogSourceNote } from "./CatalogSourceNote";

// 2026-09-18-84be: what the page is showing, in the server's own words. The phrase is
// minted by the resolution the server bound — never composed here — and the reading time
// rides with it because the contents are cached: a version printed beside contents of
// unknown age would be the next false witness rather than the cure for the last one.

export function CatalogOriginLine({ origin }: { origin: CatalogOrigin | null }) {
  if (origin === null) return null;
  return (
    <div className="originline" data-testid="catalog-origin">
      <span className="mono" data-testid="catalog-origin-phrase">
        {origin.phrase}
      </span>
      <span data-testid="catalog-origin-read-at">
        read {new Date(origin.readAt).toLocaleString()}
      </span>
      <CatalogSourceNote overlayPath={origin.overlayPath} testId="catalog-origin-source" />
    </div>
  );
}
