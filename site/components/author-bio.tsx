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
            I spent twenty years writing .NET in production. Payments, internal
            platforms, line-of-business systems where a green build did not stop
            a 2 a.m. page.
          </p>
          <p>
            The bugs that hurt me were rarely the ones tests caught. They were
            the assumptions I did not know I was making. A guard clause removed
            in a "cleanup" PR. A method renamed from <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">Delete</code> to{" "}
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">Get</code> that still deleted. A{" "}
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">{"catch {}"}</code> swallowed under cover of a 200-line reformat.
            None of these failed code review. None of them failed CI. They
            failed customers.
          </p>
          <p>
            After enough postmortems, I started writing down the checks I was
            running by hand on every diff. Look for removed lines. Look for
            signature changes without test changes. Look for new{" "}
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">throw new</code> without a matching{" "}
            <code className="rounded bg-muted px-1 py-0.5 font-mono text-xs">Assert.Throws</code>. The list grew. It became a habit. The
            habit became a tool.
          </p>
          <p>
            GauntletCI is that tool. Thirty deterministic rules, every one tied
            to a production failure I either caused or shipped through. It runs
            locally in under a second, on every diff, before code review even
            starts.
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
