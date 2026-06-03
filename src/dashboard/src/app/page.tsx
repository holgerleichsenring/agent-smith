import { RunsList } from "@/components/jobs/RunsList";

export default function JobsPage() {
  return (
    <main className="w-full space-y-6 p-8">
      <header className="space-y-1">
        <h1 className="text-[28px] font-semibold tracking-tight text-stone-900">Runs</h1>
        <p className="text-[14.5px] text-stone-400">live &mdash; newest first</p>
      </header>
      <RunsList />
    </main>
  );
}
