import { Header } from "@/components/header";
import { DocsSidebar } from "@/components/docs-sidebar";

export default function DocsLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Header />
      <div className="min-h-screen pt-16">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
          <div className="flex gap-12 py-12">
            <DocsSidebar />
            <main className="flex-1 min-w-0 prose prose-invert max-w-none">
              {children}
            </main>
          </div>
        </div>
      </div>
    </>
  );
}
