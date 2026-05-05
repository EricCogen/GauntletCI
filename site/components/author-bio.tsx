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
          Eric Cogen is a senior .NET engineer with twenty years in production. He has shipped payments systems, internal platforms, and critical line-of-business applications — the kind where a 2 a.m. alert wasn't an emergency, it was a regular Tuesday. GauntletCI is the pre-commit checklist he wishes he had run before every commit.
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
