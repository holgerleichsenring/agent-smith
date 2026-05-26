import { notFound } from "next/navigation";
import Link from "next/link";
import { getJob, getJobFile } from "@/lib/api";
import { TopologyHeader } from "@/components/jobs/TopologyHeader";
import { ArtefactSidebar } from "@/components/jobs/ArtefactSidebar";
import { ResultMarkdown } from "@/components/jobs/ResultMarkdown";

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function JobDetailPage({ params }: PageProps) {
  const { id } = await params;
  const detail = await getJob(id);
  if (!detail) notFound();

  const resultMd = (await getJobFile(id, "result.md")) ?? "(result.md not available)";

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-8">
      <Link href="/" className="text-xs text-stone-500 hover:underline">
        ← back to jobs
      </Link>
      <TopologyHeader meta={detail.meta} />
      <div className="grid grid-cols-1 gap-6 md:grid-cols-[1fr_280px]">
        <ResultMarkdown content={resultMd} />
        <ArtefactSidebar runId={detail.meta.runId} artefacts={detail.artefacts} />
      </div>
    </main>
  );
}
