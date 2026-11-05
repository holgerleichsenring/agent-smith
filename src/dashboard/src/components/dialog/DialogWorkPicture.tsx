import type { SpecDialogWorkPicture } from "@/types/spec-dialog";

// 2026-11-05-862ea: the work picture the design partner stated, on one line under the heading — what kind of
// undertaking, how large, and where its truth lives. The operator corrects it in the conversation.
const SIZES: Record<string, string> = { oneSpec: "one spec", series: "series", plan: "plan" };

export function DialogWorkPicture({ picture }: { picture: SpecDialogWorkPicture }) {
  return (
    <span data-testid="dialog-work-picture" className="d-work-picture ec-sub">
      {`Work picture: ${picture.kind} · ${SIZES[picture.size] ?? picture.size} · ${picture.truth}`}
    </span>
  );
}
