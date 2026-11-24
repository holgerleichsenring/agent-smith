"use client";

import { useT } from "@/hooks/useT";
import { sizeOf } from "@/components/dialog/referenceSelection";
import type { T } from "@/lib/i18n/translate";
import type { PocUpload } from "@/lib/pocsApi";

// 2026-11-23-933db: the checks the upload passed, in the order the server answered them. A key this page does not
// know reads as itself, so a check the server adds still shows.

export function ImportChecks({ upload }: { upload: PocUpload }) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-import-checks">
      <h2>{t("pocs.import.checks.title")}</h2>
      <ul className="poc-checks">
        {upload.checks.map((key) => {
          const { title, detail } = checkText(t, key, upload);
          return (
            <li key={key} data-testid={`poc-import-check-${key}`}>
              <svg className="poc-ok" width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor"
                strokeWidth="2" aria-hidden="true"><path d="M3 8.5l3 3 7-7" /></svg>
              <span className="poc-check-text">{title}{detail && <span className="poc-meta">{detail}</span>}</span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function checkText(t: T, key: string, upload: PocUpload): { title: string; detail: string | null } {
  switch (key) {
    case "paths": return { title: t("pocs.import.check.paths"), detail: t("pocs.import.check.paths.detail") };
    case "kinds": return { title: t("pocs.import.check.kinds"), detail: t("pocs.import.check.kinds.detail") };
    case "bounds": return {
      title: t("pocs.import.check.bounds"),
      detail: t("pocs.import.check.bounds.detail", { files: t("common.files", { count: upload.entryCount }), size: sizeOf(upload.unpackedBytes) }),
    };
    case "forbidden": return { title: t("pocs.import.check.forbidden"), detail: t("pocs.import.check.forbidden.detail") };
    case "dockerfile": return { title: t("pocs.import.check.dockerfile"), detail: t("pocs.import.check.dockerfile.detail") };
    case "expose": return { title: t("pocs.import.check.expose"), detail: t("pocs.import.check.expose.detail", { port: upload.port }) };
    default: return { title: key, detail: null };
  }
}
