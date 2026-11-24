"use client";

import { FailedSurface } from "@/components/shell/FailedSurface";
import { useLanguage, useT } from "@/hooks/useT";
import "./globals.css";

// 2026-08-25-39ab: the LAST boundary. The route boundary sits inside the root
// layout, so a throw in the layout itself — the app rail, the degraded banner,
// a shared provider — escapes it and takes the document with it. Next replaces
// the whole document with this one, which is why it renders its own <html> and
// <body>. It is the difference between a blank tab and a page that says what
// broke and offers to try again.

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useT();
  return (
    <html lang={useLanguage()}>
      <body className="bg-white p-8">
        <div data-testid="global-error">
          <FailedSurface surface={t("shell.surface.dashboard")} error={error} onRetry={reset} />
        </div>
      </body>
    </html>
  );
}
