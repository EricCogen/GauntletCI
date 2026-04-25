import Link from "next/link";
import { Shield } from "lucide-react";
import { getRule } from "@/lib/rules";

type Props = {
  ids: string[];
  title?: string;
};

export function RulesApplied({ ids, title = "Rules applied in this article" }: Props) {
  const ruleEntries = ids
    .map((id) => getRule(id))
    .filter((r): r is NonNullable<ReturnType<typeof getRule>> => Boolean(r));

  if (ruleEntries.length === 0) return null;

  return (
    <aside className="not-prose my-10 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-5">
      <div className="flex items-center gap-2 mb-3">
        <Shield className="h-4 w-4 text-cyan-400" />
        <h3 className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
          {title}
        </h3>
      </div>
      <p className="text-sm text-muted-foreground mb-4">
        GauntletCI enforces the ideas above with deterministic rules that run on
        every diff:
      </p>
      <ul className="space-y-2">
        {ruleEntries.map((rule) => (
          <li key={rule.id}>
            <Link
              href={`/docs/rules/${rule.id}`}
              className="group flex items-start gap-3 rounded-md p-2 -mx-2 hover:bg-card/60 transition-colors"
            >
              <span className="mt-0.5 inline-flex shrink-0 items-center rounded-md bg-cyan-400/10 px-2 py-0.5 text-xs font-mono font-semibold text-cyan-400 ring-1 ring-inset ring-cyan-400/20">
                {rule.id}
              </span>
              <span className="text-sm">
                <span className="font-medium text-foreground group-hover:text-cyan-400 transition-colors">
                  {rule.name}
                </span>
                <span className="text-muted-foreground"> -- {rule.description}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </aside>
  );
}
