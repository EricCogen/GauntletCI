import type { ReactNode } from "react";

type IntegrationStatusBannerProps = {
  title: string;
  children: ReactNode;
};

export function IntegrationStatusBanner({ title, children }: IntegrationStatusBannerProps) {
  return (
    <div className="rounded-xl border border-yellow-500/30 bg-yellow-500/10 p-4">
      <p className="text-sm font-semibold text-yellow-300">{title}</p>
      <p className="mt-1 text-sm leading-relaxed text-yellow-100/80">{children}</p>
    </div>
  );
}
