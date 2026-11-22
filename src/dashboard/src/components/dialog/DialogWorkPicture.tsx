import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import type { SpecDialogWorkPicture } from "@/types/spec-dialog";

// 2026-11-05-862ea: the work picture the design partner stated, on one line under the heading — what kind of
// undertaking, how large, and where its truth lives. The operator corrects it in the conversation.
const SIZES: Record<string, TextKey> = { oneSpec: "dialog.workPicture.oneSpec", series: "dialog.workPicture.series", plan: "dialog.workPicture.plan" };

export function DialogWorkPicture({ picture }: { picture: SpecDialogWorkPicture }) {
  const t = useT();
  const size = SIZES[picture.size];
  return (
    <span data-testid="dialog-work-picture" className="d-work-picture ec-sub">
      {t("dialog.workPicture", { kind: picture.kind, size: size ? t(size) : picture.size, truth: picture.truth })}
    </span>
  );
}
