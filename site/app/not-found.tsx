import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export default function NotFound() {
  return (
    <>
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-2xl px-4 sm:px-6 lg:px-8 py-24 text-center">
          <p className="text-5xl font-bold text-primary mb-4">404</p>
          <h1 className="text-2xl font-semibold text-foreground mb-3">
            Page not found
          </h1>
          <p className="text-muted-foreground mb-10">
            This page does not exist or was moved. Try one of the links below.
          </p>
          <ul className="flex flex-col sm:flex-row gap-4 justify-center text-sm">
            <li>
              <Link href="/" className="text-primary hover:underline font-medium">
                Home
              </Link>
            </li>
            <li>
              <Link href="/docs" className="text-primary hover:underline font-medium">
                Documentation
              </Link>
            </li>
            <li>
              <Link href="/detections" className="text-primary hover:underline font-medium">
                Detection rules
              </Link>
            </li>
            <li>
              <Link href="/pricing" className="text-primary hover:underline font-medium">
                Pricing
              </Link>
            </li>
            <li>
              <Link href="/benchmark" className="text-primary hover:underline font-medium">
                Benchmark
              </Link>
            </li>
          </ul>
        </div>
      </main>
      <Footer />
    </>
  );
}
