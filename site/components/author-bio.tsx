import Link from "next/link";
import { Github } from "lucide-react";

type Props = {
  variant?: "short" | "long";
};

export function AuthorBio({ variant = "short" }: Props) {
  if (variant === "long") {
    return (
      <section className="not-prose my-8 rounded-xl border border-border bg-card/40 p-5">
        <div className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted-foreground">
          About the author
        </div>
        <p className="text-base font-semibold text-foreground mb-3">
          Eric Cogen -- Founder, GauntletCI
        </p>
        <p className="text-sm leading-relaxed text-muted-foreground mb-3">
          Twenty years as a senior technical consultant and software engineer focused on application modernization, cloud-native architecture, and AI-driven software systems. He has more than two decades of experience building and modernizing enterprise platforms across .NET, C#, SQL Server, AWS, serverless architecture, microservices, CI/CD, and distributed systems. His recent work includes leading AWS-based media processing platforms, reducing transcription costs through self-hosted Whisper pipelines, and applying AI and machine learning to classification, enrichment, and semantic search workflows.
        </p>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
          <Link
            href="https://github.com/EricCogen"
            target="_blank"
            rel="noopener noreferrer me"
            className="inline-flex items-center gap-1.5 text-cyan-400 hover:underline"
          >
            <Github className="h-4 w-4" />
            @EricCogen
          </Link>
          <span className="text-muted-foreground">/</span>
          <Link
            href="https://github.com/EricCogen/GauntletCI"
            target="_blank"
            rel="noopener noreferrer"
            className="text-cyan-400 hover:underline"
          >
            GauntletCI on GitHub
          </Link>
        </div>
      </section>
    );
  }

  return (
    <section className="not-prose my-10 rounded-xl border border-border bg-card/40 p-5">
      <div className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted-foreground">
        About the author
      </div>
      <p className="text-base font-semibold text-foreground mb-2">
        Eric Cogen -- Founder, GauntletCI
      </p>
      <p className="text-sm leading-relaxed text-muted-foreground">
        Twenty years as a senior technical consultant building and modernizing enterprise platforms across .NET, AWS, serverless, microservices, and AI-driven systems.
      </p>
      <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
        <Link
          href="https://github.com/EricCogen"
          target="_blank"
          rel="noopener noreferrer me"
          className="inline-flex items-center gap-1.5 text-cyan-400 hover:underline"
        >
          <Github className="h-4 w-4" />
          @EricCogen
        </Link>
        <span className="text-muted-foreground">/</span>
        <Link
          href="https://github.com/EricCogen/GauntletCI"
          target="_blank"
          rel="noopener noreferrer"
          className="text-cyan-400 hover:underline"
        >
          GauntletCI on GitHub
        </Link>
        <span className="text-muted-foreground">/</span>
        <Link href="/about" className="text-cyan-400 hover:underline">
          More about Eric
        </Link>
      </div>
    </section>
  );
}
