/**
 * gen-og-images.mjs
 * Generates 1200x630 PNG Open Graph images for all site routes.
 * Uses sharp (already a Next.js dependency) to render SVG templates.
 *
 * Run: node scripts/gen-og-images.mjs
 */

import sharp from 'sharp';
import { mkdirSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = join(__dirname, '..', 'public', 'og');

mkdirSync(OUT_DIR, { recursive: true });

const CYAN = '#06b6d4';
const BG = '#0a0a0a';
const WHITE = '#ffffff';
const MUTED = '#555555';
const ACCENT_MUTED = '#1a3a3a';

const pages = [
  {
    slug: 'home',
    title: 'Pre-Commit Change-Risk\nDetection for .NET',
    category: '',
    sub: 'Stop behavioral regressions before they reach review.',
  },
  {
    slug: 'why-code-review-misses-bugs',
    title: 'Code Review\nBlind Spots',
    category: 'The Problem',
    sub: 'Behavioral drift, contract changes, and removed safety checks.',
  },
  {
    slug: 'why-tests-miss-bugs',
    title: 'Why Tests\nMiss Bugs',
    category: 'The Problem',
    sub: 'A green build is not the same as safe code.',
  },
  {
    slug: 'what-is-diff-based-analysis',
    title: 'What Is\nDiff-Based Analysis?',
    category: 'How It Works',
    sub: 'Analyze only the changed lines. Low noise. Directly actionable.',
  },
  {
    slug: 'detect-breaking-changes-before-merge',
    title: 'Detect Breaking Changes\nBefore Merge',
    category: 'How It Works',
    sub: 'Catch API contract violations and behavioral regressions at commit time.',
  },
  {
    slug: 'compare-sonarqube',
    title: 'GauntletCI vs\nSonarQube',
    category: 'Compare',
    sub: 'Diff-focused behavioral analysis vs whole-codebase static scanning.',
  },
  {
    slug: 'compare-snyk',
    title: 'GauntletCI vs\nSnyk',
    category: 'Compare',
    sub: 'Change-risk detection vs dependency vulnerability scanning.',
  },
  {
    slug: 'compare-codeclimate',
    title: 'GauntletCI vs\nCodeClimate',
    category: 'Compare',
    sub: 'Pre-commit behavioral analysis vs post-merge quality metrics.',
  },
  {
    slug: 'compare-codeql',
    title: 'GauntletCI vs\nCodeQL',
    category: 'Compare',
    sub: 'Deterministic diff rules vs query-based semantic analysis.',
  },
  {
    slug: 'compare-semgrep',
    title: 'GauntletCI vs\nSemgrep',
    category: 'Compare',
    sub: 'Behavioral change detection vs pattern-matching rule engine.',
  },
  {
    slug: 'pricing',
    title: 'Simple Pricing.\nNo Seats.',
    category: 'Pricing',
    sub: 'One flat rate per repository. No per-seat charges.',
  },
  {
    slug: 'detections',
    title: '33 Detection Rules\nfor .NET',
    category: 'Product',
    sub: 'Every rule targets a real category of production incident.',
  },
  {
    slug: 'docs',
    title: 'GauntletCI\nDocumentation',
    category: 'Docs',
    sub: 'Installation, configuration, rules, and CLI reference.',
  },
  {
    slug: 'can-ai-code-review-be-deterministic',
    title: 'Can AI Code Review\nBe Deterministic?',
    category: 'Article',
    sub: 'Determinism vs probabilistic judgment in code review.',
  },
];

function escapeXml(str) {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

function buildSvg({ title, category, sub }) {
  const lines = title.split('\n');
  const fontSize = lines.some((l) => l.length > 22) ? 76 : 88;
  const lineHeight = fontSize * 1.18;
  const titleY = category ? 210 : 230;

  const titleLines = lines
    .map(
      (line, i) =>
        `<text x="70" y="${titleY + i * lineHeight}"
          font-family="'Segoe UI', Arial, sans-serif"
          font-size="${fontSize}"
          font-weight="800"
          fill="${WHITE}">${escapeXml(line)}</text>`
    )
    .join('\n');

  const subY = titleY + lines.length * lineHeight + 30;

  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="630">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="${BG}"/>
      <stop offset="100%" stop-color="#0f1a1a"/>
    </linearGradient>
  </defs>

  <!-- Background -->
  <rect width="1200" height="630" fill="url(#bg)"/>

  <!-- Subtle dot grid -->
  <pattern id="dots" x="0" y="0" width="24" height="24" patternUnits="userSpaceOnUse">
    <circle cx="1" cy="1" r="1" fill="#1c1c1c"/>
  </pattern>
  <rect width="1200" height="630" fill="url(#dots)" opacity="0.6"/>

  <!-- Bottom-right cyan glow -->
  <radialGradient id="glow" cx="100%" cy="100%" r="60%">
    <stop offset="0%" stop-color="${ACCENT_MUTED}" stop-opacity="0.8"/>
    <stop offset="100%" stop-color="transparent" stop-opacity="0"/>
  </radialGradient>
  <rect width="1200" height="630" fill="url(#glow)"/>

  <!-- Left accent bar -->
  <rect x="0" y="0" width="6" height="630" fill="${CYAN}"/>

  <!-- Top line: wordmark + category -->
  <text x="70" y="90"
    font-family="'Segoe UI', Arial, sans-serif"
    font-size="22"
    font-weight="700"
    fill="${CYAN}"
    letter-spacing="2">${escapeXml('GAUNTLETCI')}${category ? `  ·  ${escapeXml(category.toUpperCase())}` : ''}</text>

  <!-- Title -->
  ${titleLines}

  <!-- Subtitle -->
  ${
    sub
      ? `<text x="70" y="${subY}"
      font-family="'Segoe UI', Arial, sans-serif"
      font-size="26"
      fill="${MUTED}">${escapeXml(sub)}</text>`
      : ''
  }

  <!-- Bottom: tagline left, URL right -->
  <text x="70" y="595"
    font-family="'Segoe UI', Arial, sans-serif"
    font-size="18"
    fill="#333333">Deterministic change-risk detection for .NET</text>
  <text x="1130" y="595"
    font-family="'Segoe UI', Arial, sans-serif"
    font-size="18"
    font-weight="600"
    fill="${CYAN}"
    text-anchor="end">gauntletci.com</text>
</svg>`;
}

let generated = 0;
let failed = 0;

for (const page of pages) {
  const svgStr = buildSvg(page);
  const outPath = join(OUT_DIR, `${page.slug}.png`);
  try {
    await sharp(Buffer.from(svgStr)).png({ compressionLevel: 9 }).toFile(outPath);
    console.log(`  ok  ${page.slug}.png`);
    generated++;
  } catch (err) {
    console.error(`  ERR ${page.slug}: ${err.message}`);
    failed++;
  }
}

console.log(`\nDone. ${generated} generated, ${failed} failed.`);
console.log(`Output: ${OUT_DIR}`);
