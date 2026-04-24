"use client";

import Link from "next/link";
import Image from "next/image";
import { Button } from "@/components/ui/button";
import { Github, Menu, X } from "lucide-react";
import { useState } from "react";

export function Header() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <header className="fixed top-0 left-0 right-0 z-50 border-b border-border bg-background/80 backdrop-blur-md">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex h-16 items-center justify-between">
          <div className="flex items-center gap-8">
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
            <nav className="hidden md:flex items-center gap-6">
              <Link href="/#features" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                Features
              </Link>
              <Link href="/#reliability" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                Proven Results
              </Link>
              <Link href="/#quickstart" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                Quick Start
              </Link>
              <Link href="/docs" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                Docs
              </Link>
            </nav>
          </div>
          <div className="hidden md:flex items-center gap-4">
            <Button variant="ghost" size="sm" asChild>
              <Link href="https://github.com/ericcogen/gauntletci" target="_blank" rel="noopener noreferrer">
                <Github className="mr-2 h-4 w-4" />
                GitHub
              </Link>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link href="/pricing">Pricing</Link>
            </Button>
            <Button variant="outline" size="sm" asChild>
              <Link href="https://github.com/EricCogen/GauntletCI-Demo/pulls" target="_blank" rel="noopener noreferrer">
                See Live Demo
              </Link>
            </Button>
            <Button size="sm" asChild>
              <Link href="/#quickstart">Get Started</Link>
            </Button>
          </div>
          <button
            className="md:hidden p-2"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            aria-label="Toggle menu"
          >
            {mobileMenuOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
          </button>
        </div>
      </div>
      {mobileMenuOpen && (
        <div className="md:hidden border-t border-border bg-background">
          <nav className="flex flex-col p-4 gap-4">
            <Link href="/#features" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              Features
            </Link>
            <Link href="/#reliability" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              Proven Results
            </Link>
            <Link href="/#quickstart" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              Quick Start
            </Link>
            <Link href="/docs" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              Docs
            </Link>
            <div className="flex flex-col gap-2 pt-4 border-t border-border">
              <Button variant="outline" size="sm" asChild>
                <Link href="https://github.com/ericcogen/gauntletci" target="_blank" rel="noopener noreferrer">
                  <Github className="mr-2 h-4 w-4" />
                  GitHub
                </Link>
              </Button>
              <Button variant="outline" size="sm" asChild>
                <Link href="/pricing" onClick={() => setMobileMenuOpen(false)}>Pricing</Link>
              </Button>
              <Button variant="outline" size="sm" asChild>
                <Link
                  href="https://github.com/EricCogen/GauntletCI-Demo/pulls"
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={() => setMobileMenuOpen(false)}
                >
                  See Live Demo
                </Link>
              </Button>
              <Button size="sm" asChild>
                <Link href="#quickstart">Get Started</Link>
              </Button>
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
