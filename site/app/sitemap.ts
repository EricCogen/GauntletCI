import { MetadataRoute } from "next";
import { rules } from "@/lib/rules";
import { articles } from "@/lib/articles";

export const dynamic = "force-static";

const BASE_URL = "https://gauntletci.com";
const PAGE_SIZE = 10;

export default function sitemap(): MetadataRoute.Sitemap {
  const ruleEntries: MetadataRoute.Sitemap = rules.map((r) => ({
    url: `${BASE_URL}/docs/rules/${r.id}`,
    changeFrequency: "monthly",
    priority: 0.7,
  }));

  const articleEntries: MetadataRoute.Sitemap = articles.map((article) => ({
    url: `${BASE_URL}${article.href}`,
    changeFrequency: "monthly",
    priority: article.slug === "corpus-report-2025" ? 0.9 : article.pinned ? 0.8 : 0.7,
  }));

  const totalArticlePages = Math.ceil(articles.length / PAGE_SIZE);

  // Pagination pages
  const paginationEntries: MetadataRoute.Sitemap = Array.from(
    { length: Math.max(totalArticlePages - 1, 0) },
    (_, i) => ({
      url: `${BASE_URL}/articles/p/${i + 2}`,
      changeFrequency: "weekly",
      priority: 0.8,
    })
  );

  return [
    { url: `${BASE_URL}/`,                                    changeFrequency: "weekly",  priority: 1.0 },
    { url: `${BASE_URL}/articles`,                            changeFrequency: "weekly",  priority: 0.9 },
    ...paginationEntries,
    { url: `${BASE_URL}/docs`,                                changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/rules`,                          changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/docs/cli-reference`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/configuration`,                  changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/integrations`,                   changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/docs/local-llm`,                      changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/docs/custom-rules`,                    changeFrequency: "monthly", priority: 0.7 },
    { url: `${BASE_URL}/demo`,                                changeFrequency: "weekly",  priority: 0.9 },
    { url: `${BASE_URL}/demo/github-checks-sarif`,             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/detections`,                          changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/pricing`,                             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/releases`,                            changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/about`,                               changeFrequency: "monthly", priority: 0.7 },
    ...articleEntries,
    { url: `${BASE_URL}/compare/gauntletci-vs-sonarqube`,     changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeql`,        changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-semgrep`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-snyk`,          changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-codeclimate`,   changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-ndepend`,       changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/compare/gauntletci-vs-ai-code-review`, changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies`,                                                     changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/benchmark`,                                                         changeFrequency: "monthly", priority: 0.9 },
    { url: `${BASE_URL}/articles/case-studies/stackexchange-redis-swallowed-exception`,             changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/newtonsoft-json-assignment-in-getter`,                changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/efcore-breaking-api-removal`,                        changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/nunit-thread-sleep-async`,                           changeFrequency: "monthly", priority: 0.8 },
    { url: `${BASE_URL}/articles/case-studies/azuread-hardcoded-authority`,                        changeFrequency: "monthly", priority: 0.8 },
    ...ruleEntries,
  ];
}
