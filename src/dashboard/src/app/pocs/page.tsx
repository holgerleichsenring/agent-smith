import { PocsHome } from "@/components/pocs/PocsHome";

// 2026-11-23-933da: the PoCs list, in the ticket list's frame.
export default function PocsPage() {
  return (
    <div className="mock-shell mock-tickets mock-pocs" data-testid="pocs-home">
      <main className="main">
        <PocsHome />
      </main>
    </div>
  );
}
