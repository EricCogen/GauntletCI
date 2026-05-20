type Source = {
  label: string;
  href: string;
  description: string;
};

type SourcesSectionProps = {
  sources: Source[];
  intro?: string;
};

export function SourcesSection({ sources, intro }: SourcesSectionProps) {
  return (
    <section className="space-y-4 rounded-xl border border-border bg-card/50 p-6">
      <h2 className="text-2xl font-bold">Sources and scope</h2>
      <p className="text-sm text-muted-foreground leading-relaxed">
        {intro ??
          "This article combines cited public documentation with GauntletCI's product positioning and engineering analysis. Tool capability claims are limited to the linked vendor documentation."}
      </p>
      <ul className="space-y-3">
        {sources.map((source) => (
          <li key={source.href} className="text-sm text-muted-foreground leading-relaxed">
            <a
              href={source.href}
              className="font-medium text-cyan-400 hover:text-cyan-300 underline underline-offset-2"
            >
              {source.label}
            </a>
            {" — "}
            {source.description}
          </li>
        ))}
      </ul>
    </section>
  );
}
