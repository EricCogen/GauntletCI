import Link from "next/link";
import Image from "next/image";
import { Github, Twitter, Linkedin, MessageSquare } from "lucide-react";

export function Footer() {
  return (
    <footer data-pagefind-ignore="" className="border-t border-border bg-card/50">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="py-12">
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-8">
            <div>
              <Link href="/" className="flex items-center gap-2">
                <Image
                  src="/logo.png"
                  alt="GauntletCI logo"
                  width={96}
                  height={126}
                  className="h-8 w-auto"
                />
                <span className="text-lg font-semibold tracking-tight">GauntletCI</span>
              </Link>
              <p className="mt-4 text-sm text-muted-foreground max-w-xs">
                Pre-commit change-risk detection for pull request diffs. 
                Catch behavioral changes before code review.
              </p>
            </div>
            
            <div className="grid grid-cols-2 lg:grid-cols-4 gap-8 sm:gap-16">
              <div>
                <h3 className="text-sm font-semibold mb-4">Product</h3>
                <ul className="space-y-3">
                  <li>
                    <Link href="/#features" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Features
                    </Link>
                  </li>
                  <li>
                    <Link href="/#quickstart" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Quick Start
                    </Link>
                  </li>
                  <li>
                    <Link href="/docs" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Docs
                    </Link>
                  </li>
                </ul>
              </div>
              <div>
                <h3 className="text-sm font-semibold mb-4">Resources</h3>
                <ul className="space-y-3">
                  <li>
                    <Link href="/about" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      About
                    </Link>
                  </li>
                  <li>
                    <Link href="/articles" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Articles
                    </Link>
                  </li>
                  <li>
                    <Link href="/case-studies" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Case Studies
                    </Link>
                  </li>
                </ul>
              </div>
              <div>
                <h3 className="text-sm font-semibold mb-4">Community</h3>
                <ul className="space-y-3">
                  <li>
                    <Link
                      href="https://github.com/EricCogen/GauntletCI-Demo/pulls"
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-sm text-muted-foreground hover:text-foreground transition-colors"
                    >
                      Live Demo
                    </Link>
                  </li>
                  <li>
                    <Link href="https://github.com/ericcogen/gauntletci" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      GitHub
                    </Link>
                  </li>
                  <li>
                    <Link href="https://github.com/ericcogen/gauntletci/issues" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Issues
                    </Link>
                  </li>
                </ul>
              </div>
              <div>
                <h3 className="text-sm font-semibold mb-4">More</h3>
                <ul className="space-y-3">
                  <li>
                    <Link href="/releases" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                      Releases
                    </Link>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
        
        <div className="border-t border-border py-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-6">
          <div className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              Elastic License 2.0. Built by{" "}
              <Link href="/about" className="text-foreground hover:text-cyan-400 transition-colors">
                Eric Cogen
              </Link>
            </p>
            <div className="flex items-center gap-4">
              <Link
                href="https://github.com/ericcogen/gauntletci"
                target="_blank"
                rel="noopener noreferrer"
                title="GitHub"
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                <Github className="h-5 w-5" />
              </Link>
              <Link
                href="https://twitter.com/GauntletCI_BCRV"
                target="_blank"
                rel="noopener noreferrer"
                title="Twitter"
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                <Twitter className="h-5 w-5" />
              </Link>
              <Link
                href="https://github.com/ericcogen/gauntletci/discussions"
                target="_blank"
                rel="noopener noreferrer"
                title="Discussions"
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                <MessageSquare className="h-5 w-5" />
              </Link>
            </div>
          </div>
          <Link
            href="https://github.com/ericcogen/gauntletci"
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            <Github className="h-4 w-4" />
            <span>Star on GitHub</span>
          </Link>
        </div>
      </div>
    </footer>
  );
}
