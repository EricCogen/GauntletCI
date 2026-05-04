import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { parseChangelog } from "@/lib/parseChangelog";

export const metadata: Metadata = {
  title: "Releases | GauntletCI",
  description:
    "GauntletCI version history. See what changed in each release - new detection rules, CLI improvements, and precision hardening.",
  alternates: { canonical: "/releases" },
};

const releases = parseChangelog();

const sectionColors: Record<string, string> = {
  Added: "text-emerald-400 border-emerald-500/30 bg-emerald-500/10",
  Changed: "text-blue-400 border-blue-500/30 bg-blue-500/10",
  Fixed: "text-amber-400 border-amber-500/30 bg-amber-500/10",
};

export default function ReleasesPage() {
  return (
    <>
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8 py-12">
          <Link href="/" className="text-sm text-muted-foreground hover:text-foreground">
            ← Back home
          </Link>

          <h1 className="mt-6 text-4xl font-bold tracking-tight">Releases</h1>
          <p className="mt-3 text-lg text-muted-foreground">
            What changed in each version of GauntletCI.
          </p>

          <p className="mt-2 text-sm text-muted-foreground">
            Full technical changelog:{" "}
            <Link
              href="https://github.com/EricCogen/GauntletCI/blob/main/CHANGELOG.md"
              target="_blank"
              rel="noopener noreferrer"
              className="text-cyan-400 hover:underline"
            >
              CHANGELOG.md on GitHub
            </Link>
          </p>

          <div className="mt-12 space-y-12">
            {releases.map((release) => (
              <div key={release.version} className="relative pl-6 border-l border-border">
                <div className="absolute -left-2.5 top-1 h-5 w-5 rounded-full border border-border bg-background flex items-center justify-center">
                  <div className="h-2 w-2 rounded-full bg-cyan-400" />
                </div>

                <div className="flex flex-wrap items-center gap-3 mb-3">
                  <h2 className="text-xl font-bold text-foreground">v{release.version}</h2>
                  {release.tag === "latest" && (
                    <span className="text-xs font-medium rounded-full px-2.5 py-0.5 border border-cyan-500/30 bg-cyan-500/10 text-cyan-400">
                      latest
                    </span>
                  )}
                  {release.tag === "initial" && (
                    <span className="text-xs font-medium rounded-full px-2.5 py-0.5 border border-violet-500/30 bg-violet-500/10 text-violet-400">
                      initial release
                    </span>
                  )}
                  <span className="text-sm text-muted-foreground">{release.date}</span>
                  {release.compareUrl && (
                    <Link
                      href={release.compareUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-xs text-muted-foreground hover:text-foreground"
                    >
                      diff →
                    </Link>
                  )}
                </div>

                <p className="text-muted-foreground text-sm mb-5">{release.summary}</p>

                <div className="space-y-4">
                  {release.sections.map((section) => (
                    <div key={section.label}>
                      <span
                        className={`inline-block text-xs font-medium rounded-full px-2.5 py-0.5 border mb-3 ${sectionColors[section.label]}`}
                      >
                        {section.label}
                      </span>
                      <ul className="space-y-2">
                        {section.entries.map((entry, i) => (
                          <li key={i} className="flex items-start gap-2 text-sm text-muted-foreground">
                            <span className="mt-1.5 h-1.5 w-1.5 rounded-full bg-muted-foreground/40 shrink-0" />
                            {entry.link ? (
                              <span>
                                {entry.text.split(".")[0]}.{" "}
                                <Link
                                  href={entry.link}
                                  className="text-cyan-400 hover:underline"
                                  {...(entry.link.startsWith("http")
                                    ? { target: "_blank", rel: "noopener noreferrer" }
                                    : {})}
                                >
                                  View →
                                </Link>
                              </span>
                            ) : (
                              <span>{entry.text}</span>
                            )}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>

          <div className="mt-16 pt-8 border-t border-border">
            <p className="text-sm text-muted-foreground">
              Install the latest version:{" "}
              <code className="text-foreground/80 bg-muted px-1.5 py-0.5 rounded text-xs">
                dotnet tool install -g GauntletCI
              </code>
            </p>
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}
