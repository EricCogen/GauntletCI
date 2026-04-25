"use client";

import Link from "next/link";
import Image from "next/image";
import { Button } from "@/components/ui/button";
import { Github, Menu, X, ChevronDown } from "lucide-react";
import { useState, useRef, useEffect } from "react";
import { SearchDialog } from "@/components/search-dialog";

export function Header() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [productOpen, setProductOpen] = useState(false);
  const productRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (productRef.current && !productRef.current.contains(e.target as Node)) {
        setProductOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <header data-pagefind-ignore="" className="fixed top-0 left-0 right-0 z-50 border-b border-border bg-background/80 backdrop-blur-md">
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
              <div ref={productRef} className="relative">
                <button
                  onClick={() => setProductOpen((prev) => !prev)}
                  className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
                >
                  Product
                  <ChevronDown className={`h-3.5 w-3.5 transition-transform ${productOpen ? "rotate-180" : ""}`} />
                </button>
                {productOpen && (
                  <div className="absolute top-full left-0 mt-2 w-44 rounded-lg border border-border bg-card shadow-lg py-1 z-10">
                    <Link
                      href="/#features"
                      className="block px-4 py-2 text-sm text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                      onClick={() => setProductOpen(false)}
                    >
                      Features
                    </Link>
                    <Link
                      href="/#reliability"
                      className="block px-4 py-2 text-sm text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                      onClick={() => setProductOpen(false)}
                    >
                      Proven Results
                    </Link>
                    <Link
                      href="/#quickstart"
                      className="block px-4 py-2 text-sm text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                      onClick={() => setProductOpen(false)}
                    >
                      Quick Start
                    </Link>
                  </div>
                )}
              </div>
              <Link href="/docs" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                Docs
              </Link>
              <Link href="/about" className="text-sm text-muted-foreground hover:text-foreground transition-colors">
                About
              </Link>
            </nav>
          </div>
          <div className="hidden md:flex items-center gap-4">
            <SearchDialog />
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
            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest">Product</p>
            <Link href="/#features" className="text-sm text-muted-foreground hover:text-foreground pl-2" onClick={() => setMobileMenuOpen(false)}>
              Features
            </Link>
            <Link href="/#reliability" className="text-sm text-muted-foreground hover:text-foreground pl-2" onClick={() => setMobileMenuOpen(false)}>
              Proven Results
            </Link>
            <Link href="/#quickstart" className="text-sm text-muted-foreground hover:text-foreground pl-2" onClick={() => setMobileMenuOpen(false)}>
              Quick Start
            </Link>
            <Link href="/docs" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              Docs
            </Link>
            <Link href="/about" className="text-sm text-muted-foreground hover:text-foreground" onClick={() => setMobileMenuOpen(false)}>
              About
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
