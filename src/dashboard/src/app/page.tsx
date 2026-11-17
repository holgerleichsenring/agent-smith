import { HomeView } from "@/components/home/HomeView";

// 2026-11-17-d6fba: home is input — what should we work on, what waits on you, the tickets that moved last. The runs
// list lives at /runs.
export default function HomePage() {
  return (
    <div className="mock-shell mock-home" data-testid="home-page">
      <main className="main">
        <HomeView />
      </main>
    </div>
  );
}
