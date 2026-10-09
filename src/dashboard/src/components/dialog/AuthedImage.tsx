"use client";

import { useBlobUrl } from "@/hooks/useBlobUrl";
import { fetchBlob } from "@/lib/referenceFilesApi";

// 2026-10-09-86e1: an image the API serves, drawn from bytes fetched with the bearer token —
// the one way it renders with sign-in on. Until they arrive the box is empty; a failed read
// says so in place of a broken-image glyph.

export function AuthedImage({ path, alt, className, testId }: { path: string; alt: string; className?: string; testId?: string }) {
  const { url, failed } = useBlobUrl(() => fetchBlob(path), path);
  if (failed) return <span className="ec-sub" data-testid={testId}>{`${alt} — could not be loaded`}</span>;
  if (!url) return <span className="ec-sub" data-testid={testId} aria-busy="true">loading…</span>;
  return <img data-testid={testId} src={url} alt={alt} className={className} />;
}
