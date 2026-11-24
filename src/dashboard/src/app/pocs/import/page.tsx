import { PocImport } from "@/components/pocs/import/PocImport";

// 2026-11-23-933db: importing a PoC ZIP, in the PoCs list's frame.
export default function PocImportPage() {
  return (
    <div className="mock-shell mock-tickets mock-pocs" data-testid="poc-import-page">
      <main className="main">
        <PocImport />
      </main>
    </div>
  );
}
