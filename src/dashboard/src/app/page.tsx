import { RunsList } from "@/components/jobs/RunsList";

export default function JobsPage() {
  return (
    <main className="w-full space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="dsh-h1 font-semibold tracking-tight text-stone-900">Runs</h1>
        <p className="dsh-body text-stone-400">live &mdash; newest first</p>
      </header>
      <RunsList />
    </main>
  );
}
