import Link from "next/link";

export function IntegrationRelatedLinks() {
  return (
    <section>
      <h2 className="text-2xl font-semibold mb-3">Related</h2>
      <p className="text-sm text-muted-foreground">
        <Link href="/docs/integrations" className="text-cyan-400 hover:underline">
          All integrations
        </Link>
        {" · "}
        <Link href="/docs/integrations/github-action" className="text-cyan-400 hover:underline">
          GitHub Action
        </Link>
        {" · "}
        <Link href="/docs/integrations/pre-commit" className="text-cyan-400 hover:underline">
          Pre-commit hook
        </Link>
      </p>
    </section>
  );
}
