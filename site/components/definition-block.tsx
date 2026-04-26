export function DefinitionBlock() {
  return (
    <section className="py-10 border-t border-border bg-background">
      <div className="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8">
        <p className="text-sm text-muted-foreground leading-relaxed">
          <strong className="text-foreground">GauntletCI</strong> is a local-first Behavioral Change Risk engine for C# and .NET.
          It analyzes pull request diffs to catch breaking changes, behavioral regressions, and unverified logic
          shifts that pass tests and code review. Detection is deterministic, diff-scoped, and designed to run
          in under one second. Optional offline AI explanations run locally and never send your code to an
          external service.
        </p>
      </div>
    </section>
  );
}
