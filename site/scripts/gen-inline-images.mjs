/**
 * gen-inline-images.mjs
 * Generates DALL-E 3 hero illustrations for each article.
 * Saves 1792x1024 PNG to site/public/articles/{slug}-hero.png
 *
 * Run: node scripts/gen-inline-images.mjs
 * Requires: OPENAI_API_KEY env var OR ~/.gauntletci/chatgpt-api.token
 */

import { writeFileSync, mkdirSync, readFileSync, existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { homedir } from 'os';

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT_DIR = join(__dirname, '..', 'public', 'articles');
mkdirSync(OUT_DIR, { recursive: true });

// Load API key
let apiKey = process.env.OPENAI_API_KEY;
if (!apiKey) {
  const tokenPath = join(homedir(), '.gauntletci', 'chatgpt-api.token');
  if (existsSync(tokenPath)) {
    apiKey = readFileSync(tokenPath, 'utf8').replace(/\r?\n/g, '').trim();
  }
}
if (!apiKey) {
  console.error('ERROR: No API key found. Set OPENAI_API_KEY or place key in ~/.gauntletci/chatgpt-api.token');
  process.exit(1);
}

const STYLE =
  'Flat technical illustration, dark background (#0a0a0a), cyan accent highlights (#06b6d4), minimal geometric shapes, no text, no labels, no humans, abstract conceptual art, software engineering theme, clean and modern.';

const articles = [
  {
    slug: 'why-code-review-misses-bugs',
    prompt: `${STYLE} Concept: A code diff on a dark screen with lines highlighted in green (additions) and red (deletions). A critical deleted line -- a safety check -- is shown fading into shadow, nearly invisible, while reviewer attention flows toward the green additions. Symbolic: the absence of something important going unnoticed.`,
  },
  {
    slug: 'why-tests-miss-bugs',
    prompt: `${STYLE} Concept: A row of glowing green checkmarks representing passing tests, but beneath them, a hairline fracture runs through the foundation. A small bug symbol hides just outside the test coverage boundary, in a gap between two test cases. Symbolic: green status masking hidden structural risk.`,
  },
  {
    slug: 'what-is-diff-based-analysis',
    prompt: `${STYLE} Concept: Two abstract code blocks side by side. One is entirely illuminated (whole-file scan, noisy). The other has a precise focused spotlight on only 3 changed lines, everything else dim. A magnifying lens over just the delta. Symbolic: precision over breadth.`,
  },
  {
    slug: 'detect-breaking-changes-before-merge',
    prompt: `${STYLE} Concept: A merge arrow flowing toward a branch junction, stopped by a glowing amber gate. On one side of the gate, a broken chain link and a fractured API interface icon. On the other side, a clean protected codebase. Symbolic: catching breaking changes before they cross the threshold.`,
  },
];

async function generateImage(article) {
  const outPath = join(OUT_DIR, `${article.slug}-hero.png`);

  console.log(`  Requesting: ${article.slug}...`);

  const response = await fetch('https://api.openai.com/v1/images/generations', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      model: 'dall-e-3',
      prompt: article.prompt,
      n: 1,
      size: '1792x1024',
      quality: 'standard',
      response_format: 'b64_json',
    }),
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`API error ${response.status}: ${err}`);
  }

  const data = await response.json();
  const b64 = data.data[0].b64_json;
  const buf = Buffer.from(b64, 'base64');
  writeFileSync(outPath, buf);

  const kb = Math.round(buf.length / 1024);
  console.log(`  ok  ${article.slug}-hero.png (${kb} KB)`);
  return outPath;
}

let ok = 0;
let failed = 0;

for (const article of articles) {
  try {
    await generateImage(article);
    ok++;
    // Small delay between requests to be polite
    await new Promise((r) => setTimeout(r, 1000));
  } catch (err) {
    console.error(`  ERR ${article.slug}: ${err.message}`);
    failed++;
  }
}

console.log(`\nDone. ${ok} generated, ${failed} failed.`);
console.log(`Output: ${OUT_DIR}`);
