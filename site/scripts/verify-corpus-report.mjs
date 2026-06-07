import { readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const siteRoot = join(__dirname, '..');
const repoRoot = join(siteRoot, '..');
const csvPath = join(repoRoot, 'data', 'corpus-fixtures.csv');

const filesToCheck = [
  join(siteRoot, 'app', 'articles', 'corpus-report-2025', 'page.tsx'),
  join(siteRoot, 'app', 'articles', 'case-studies', 'page.tsx'),
  join(siteRoot, 'lib', 'articles.ts'),
  join(siteRoot, 'scripts', 'gen-og-images.mjs'),
];

function parseCsv(text) {
  const rows = [];
  let row = [];
  let value = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i += 1) {
    const char = text[i];
    const next = text[i + 1];

    if (char === '"' && inQuotes && next === '"') {
      value += '"';
      i += 1;
      continue;
    }

    if (char === '"') {
      inQuotes = !inQuotes;
      continue;
    }

    if (char === ',' && !inQuotes) {
      row.push(value);
      value = '';
      continue;
    }

    if ((char === '\n' || char === '\r') && !inQuotes) {
      if (char === '\r' && next === '\n') i += 1;
      row.push(value);
      if (row.some((cell) => cell.length > 0)) rows.push(row);
      row = [];
      value = '';
      continue;
    }

    value += char;
  }

  if (value.length > 0 || row.length > 0) {
    row.push(value);
    rows.push(row);
  }

  const [header, ...records] = rows;
  return records.map((record) =>
    Object.fromEntries(header.map((name, index) => [name, record[index] ?? '']))
  );
}

function number(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new Error(`Expected numeric value, got "${value}"`);
  }
  return parsed;
}

function formatInt(value) {
  return new Intl.NumberFormat('en-US').format(value);
}

function assertEqual(label, actual, expected) {
  if (actual !== expected) {
    throw new Error(`${label}: expected ${expected}, got ${actual}`);
  }
}

function assertContains(filePath, expected) {
  const text = readFileSync(filePath, 'utf8');
  if (!text.includes(expected)) {
    throw new Error(`${filePath} is missing expected corpus metric text: ${expected}`);
  }
}

const rows = parseCsv(readFileSync(csvPath, 'utf8'));
const repoCount = new Set(rows.map((row) => row.repo)).size;
const ruleIds = new Set(
  rows.flatMap((row) => row.rule_ids_triggered.split(';').filter(Boolean))
);
const highConfidenceRuleIds = new Set(
  rows.flatMap((row) => row.high_confidence_rule_ids_triggered.split(';').filter(Boolean))
);
const rawFindings = rows.reduce((sum, row) => sum + number(row.total_bcr_findings), 0);
const highConfidenceFindings = rows.reduce(
  (sum, row) => sum + number(row.high_confidence_bcr_findings),
  0
);
const riskPrs = rows.filter((row) => number(row.total_bcr_findings) > 0).length;
const highConfidencePrs = rows.filter((row) => number(row.high_confidence_bcr_findings) > 0).length;
const noTestRows = rows.filter((row) => number(row.has_tests_changed) === 0);
const noTestPrs = noTestRows.length;
const noTestRiskPrs = noTestRows.filter((row) => number(row.total_bcr_findings) > 0).length;
const noTestHighConfidencePrs = noTestRows.filter(
  (row) => number(row.high_confidence_bcr_findings) > 0
).length;
const azureRow = rows.find(
  (row) => row.repo === 'Azure/azure-sdk-for-net' && row.pr_number === '57223'
);

if (!azureRow) {
  throw new Error('Missing Azure/azure-sdk-for-net PR #57223 in corpus CSV');
}

const azureRawFindings = number(azureRow.total_bcr_findings);
const azureHighConfidenceFindings = number(azureRow.high_confidence_bcr_findings);
const azureRawPercent = ((azureRawFindings / rawFindings) * 100).toFixed(1);
const azureHighPercent = ((azureHighConfidenceFindings / highConfidenceFindings) * 100).toFixed(1);
const highConfidencePrPercent = ((highConfidencePrs / rows.length) * 100).toFixed(1);
const noTestRiskPercent = ((noTestRiskPrs / noTestPrs) * 100).toFixed(1);

assertEqual('CSV row count', rows.length, 610);
assertEqual('CSV repository count', repoCount, 61);
assertEqual('CSV distinct rule IDs', ruleIds.size, 28);
assertEqual('CSV high-confidence distinct rule IDs', highConfidenceRuleIds.size, 15);
assertEqual('CSV raw findings', rawFindings, 147958);
assertEqual('CSV high-confidence findings', highConfidenceFindings, 35871);
assertEqual('CSV PRs with findings', riskPrs, 529);
assertEqual('CSV PRs with high-confidence findings', highConfidencePrs, 214);
assertEqual('CSV PRs without test changes', noTestPrs, 178);
assertEqual('CSV no-test PRs with findings', noTestRiskPrs, 131);
assertEqual('CSV no-test PRs with high-confidence findings', noTestHighConfidencePrs, 46);
assertEqual('Azure SDK PR #57223 raw findings', azureRawFindings, 40155);
assertEqual('Azure SDK PR #57223 high-confidence findings', azureHighConfidenceFindings, 16611);
assertEqual('Azure SDK raw percentage', azureRawPercent, '27.1');
assertEqual('Azure SDK high-confidence percentage', azureHighPercent, '46.3');
assertEqual('High-confidence PR percentage', highConfidencePrPercent, '35.1');
assertEqual('No-test PR risk percentage', noTestRiskPercent, '73.6');

const values = {
  prCount: formatInt(rows.length),
  repoCount: formatInt(repoCount),
  ruleCount: formatInt(ruleIds.size),
  highConfidenceRuleCount: formatInt(highConfidenceRuleIds.size),
  rawFindings: formatInt(rawFindings),
  highConfidenceFindings: formatInt(highConfidenceFindings),
  riskPrs: formatInt(riskPrs),
  highConfidencePrs: formatInt(highConfidencePrs),
  noTestPrs: formatInt(noTestPrs),
  noTestRiskPrs: formatInt(noTestRiskPrs),
  noTestHighConfidencePrs: formatInt(noTestHighConfidencePrs),
  azureRawFindings: formatInt(azureRawFindings),
  azureHighConfidenceFindings: formatInt(azureHighConfidenceFindings),
};

const [corpusPage, caseStudiesPage, articlesRegistry, ogScript] = filesToCheck;

[
  `${values.prCount} merged C# pull requests across ${values.repoCount} repositories`,
  `{ value: "${values.prCount}", label: "merged C# PRs" }`,
  `{ value: "${values.repoCount}", label: "public repositories" }`,
  `{ value: "${values.rawFindings}", label: "raw BCR findings" }`,
  `{ value: "${values.highConfidenceFindings}", label: "high-confidence findings" }`,
  `${values.azureRawFindings} raw findings and ${values.azureHighConfidenceFindings} high-confidence findings`,
  `${azureRawPercent}% of the corpus raw total and ${azureHighPercent}% of the high-confidence total`,
  `The corpus contains ${values.noTestPrs} PRs with no test-file changes recorded. Of those, ${values.noTestRiskPrs} had at least one Behavioral Change Risk finding, and ${values.noTestHighConfidencePrs} had at least one high-confidence finding.`,
  `${values.rawFindings} triggered findings across ${values.riskPrs} PRs and ${values.ruleCount} rule IDs`,
].forEach((expected) => assertContains(corpusPage, expected));

assertContains(
  caseStudiesPage,
  `${values.prCount} merged C# pull requests, ${values.repoCount} repositories, ${values.rawFindings} raw findings`
);
assertContains(caseStudiesPage, `${values.highConfidenceFindings} high-confidence findings`);
assertContains(
  articlesRegistry,
  `${values.prCount} merged C# PRs across ${values.repoCount} repositories`
);
assertContains(
  ogScript,
  `${values.rawFindings} risk signals across ${values.prCount} merged C# PRs.`
);

[
  [
    join(siteRoot, 'app', 'articles', 'behavioral-change-risk-formal-framework', 'page.tsx'),
    [
      `${values.prCount} pull requests from ${values.repoCount} open-source .NET repositories`,
      `${highConfidencePrPercent}%`,
      `${values.highConfidencePrs} of ${values.prCount}`,
      `${values.highConfidenceRuleCount} distinct rule categories`,
      `${noTestRiskPercent}%`,
      `${values.noTestRiskPrs} of ${values.noTestPrs}`,
    ],
  ],
  [
    join(siteRoot, 'app', 'articles', 'the-asymmetry-of-change', 'page.tsx'),
    [
      `${values.prCount} pull requests across ${values.repoCount} open-source .NET repositories`,
      `${values.noTestRiskPrs} of ${values.noTestPrs}`,
      `${noTestRiskPercent}%`,
    ],
  ],
].forEach(([filePath, expectedValues]) => {
  expectedValues.forEach((expected) => assertContains(filePath, expected));
});

[
  '598 pull requests',
  '57 open-source .NET repositories',
  '207 of 598',
  '118 of 166',
].forEach((staleText) => {
  filesToCheck
    .concat([
      join(siteRoot, 'app', 'articles', 'behavioral-change-risk-formal-framework', 'page.tsx'),
      join(siteRoot, 'app', 'articles', 'the-asymmetry-of-change', 'page.tsx'),
    ])
    .forEach((filePath) => {
      const text = readFileSync(filePath, 'utf8');
      if (text.includes(staleText)) {
        throw new Error(`${filePath} contains stale corpus metric text: ${staleText}`);
      }
    });
});

console.log(
  `Corpus report metrics verified: ${values.prCount} PRs, ${values.repoCount} repos, ${values.ruleCount} rules, ${values.rawFindings} raw findings, ${values.highConfidenceFindings} high-confidence findings.`
);
