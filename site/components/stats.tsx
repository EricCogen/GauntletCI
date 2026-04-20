export function Stats() {
  const stats = [
    { value: "<1s", label: "Analysis time" },
    { value: "≤3", label: "Findings per run" },
    { value: "28", label: "Detection rules" },
    { value: "100%", label: "Local execution" },
  ];

  return (
    <section className="border-y border-border bg-card/50">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 lg:grid-cols-4 divide-x divide-y lg:divide-y-0 divide-border">
          {stats.map((stat, index) => (
            <div key={index} className="flex flex-col items-center justify-center py-8 sm:py-12">
              <span className="text-3xl sm:text-4xl font-bold text-foreground">{stat.value}</span>
              <span className="mt-2 text-sm text-muted-foreground">{stat.label}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
