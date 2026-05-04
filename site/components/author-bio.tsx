import Link from "next/link";
import { Github } from "lucide-react";

type Props = {
  variant?: "short" | "long";
};

export function AuthorBio({ variant = "short" }: Props) {
  if (variant === "long") {
    return (
      <section className="not-prose my-12 rounded-xl border border-border bg-card/40 p-6">
        <div className="mb-3 text-xs font-semibold uppercase tracking-widest text-muted-foreground">
          About the author
        </div>
        <h2 className="text-xl font-semibold text-foreground mb-4">
          Eric Cogen -- Founder, GauntletCI
        </h2>
        <div className="space-y-4 text-sm leading-relaxed text-muted-foreground">
          <p>
            Twenty years in .NET production. Payments systems, internal platforms, critical line-of-business applications—the kind where a 2 a.m. alert wasn't an emergency, it was a regular Tuesday.
          </p>
          <p>
            I learned something painful: the bugs that destroy production aren't the ones your tests catch. They're the invisible assumptions. A guard clause removed in a "cleanup" PR. A method renamed from <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">Delete</code> to{" "}
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">Get</code> that still deleted everything. An empty <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">{"catch {}"}</code> buried in a 200-line reformat. Every single one passed code review. Every single one passed CI. Every single one failed your customers.
          </p>
          <p>
            After too many postmortems, I started keeping a list. The checks I ran by hand before every diff. Look for removed guard clauses. Look for deleted lines. Look for signature changes with no corresponding test changes. Look for new exceptions without matching assertions. Watch for renames that might swap semantics. Check for silent failures hiding in error handling. The list kept growing. It became a habit. The habit became an obsession. The obsession became a tool.
          </p>
          <p>
            GauntletCI is that tool. Thirty deterministic rules, every one hardened by a real production failure I either caused or shipped. It runs locally in under a second—before code review, before CI, before your team's judgment can get tired. The rules don't sleep. The rules don't skip the boring checks. The rules catch what you can't see coming.
          </p>
        </div>
        <div className="mt-5 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
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
        Twenty years in .NET production. Most of those years, the bugs that
        hurt me were not the ones tests caught. They were the assumptions I
        did not know I was making: a removed guard clause, a renamed method
        that still did the old thing, a <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">{"catch {}"}</code>{" "}
        that turned a page into a silent dashboard lie. GauntletCI is the
        checklist I wish I had run before every commit. It runs the rules I
        learned the hard way, so you do not have to.
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
