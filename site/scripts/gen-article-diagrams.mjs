/**
 * gen-article-diagrams.mjs
 * Generates conceptual SVG→PNG diagrams for all 4 articles.
 * Each diagram presents a visual argument that the prose and code blocks cannot:
 * proportion, contrast, gap, or comparison. None reproduce inline code.
 * Run with: npm run gen-diagrams
 */
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const sharp = (await import('sharp')).default;

// Colour palette
const P = {
  bg:       '#0f172a',
  card:     '#1e293b',
  border:   '#334155',
  cyan:     '#06b6d4',
  cyanDim:  '#0891b2',
  green:    '#22c55e',
  greenDim: '#166534',
  greenBg:  '#052e16',
  red:      '#ef4444',
  redDim:   '#991b1b',
  redBg:    '#450a0a',
  amber:    '#f59e0b',
  amberBg:  '#451a03',
  text:     '#f1f5f9',
  muted:    '#64748b',
  dim:      '#334155',
};

function titleBar(W, label) {
  return `
  <rect width="${W}" height="48" fill="${P.card}" rx="8"/>
  <rect y="38" width="${W}" height="10" fill="${P.card}"/>
  <text x="${W / 2}" y="30" font-family="sans-serif" font-size="16" font-weight="700"
    fill="${P.text}" text-anchor="middle">${label}</text>`;
}

// ---------------------------------------------------------------------------
// DIAGRAM 1: Why Code Review Misses Bugs  (560 × 296)
// Two-column comparison: categories review catches vs. structural blind spots.
// Does NOT reproduce the UserService.cs diff -- that is already in the article.
// ---------------------------------------------------------------------------
function makeDiagram1() {
  const W = 560, H = 296;
  const COL_W = 264, GAP = 28, PAD = 8, ROW_H = 60;
  const SY = 92; // items start y

  const left = [
    ['Naming and style issues',     'flagged during inline review'],
    ['Added code paths',            'visible, green, easy to spot'],
    ['Inline logic errors',         'clear in diff context'],
  ];
  const right = [
    ['Removed guard clauses',       'deletions read as clean'],
    ['Deleted error handlers',      'removed code vanishes from view'],
    ['Async anti-patterns',         'context-dependent, easy to miss'],
  ];

  const makeRows = (items, xStart, bgFill, iconColor, textColor, subColor) =>
    items.map(([title, sub], i) => {
      const y = SY + i * ROW_H;
      return `
  <rect x="${xStart}" y="${y}" width="${COL_W}" height="${ROW_H - 8}" fill="${bgFill}" rx="4"/>
  <text x="${xStart + 20}" y="${y + 30}" font-family="sans-serif" font-size="26" fill="${iconColor}">&#x2713;</text>
  <text x="${xStart + 50}" y="${y + 22}" font-family="sans-serif" font-size="16" fill="${textColor}">${title}</text>
  <text x="${xStart + 50}" y="${y + 41}" font-family="sans-serif" font-size="12" fill="${subColor}">${sub}</text>`;
    }).join('');

  const makeRightRows = (items, xStart) =>
    items.map(([title, sub], i) => {
      const y = SY + i * ROW_H;
      return `
  <rect x="${xStart}" y="${y}" width="${COL_W}" height="${ROW_H - 8}" fill="${P.redBg}" rx="4"/>
  <text x="${xStart + 20}" y="${y + 30}" font-family="sans-serif" font-size="26" fill="${P.red}">&#x2717;</text>
  <text x="${xStart + 50}" y="${y + 22}" font-family="sans-serif" font-size="16" fill="#fee2e2">${title}</text>
  <text x="${xStart + 50}" y="${y + 41}" font-family="sans-serif" font-size="12" fill="#f87171">${sub}</text>`;
    }).join('');

  const lX = PAD, rX = PAD + COL_W + GAP;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W * 2}" height="${H * 2}" viewBox="0 0 ${W} ${H}">
  <rect width="${W}" height="${H}" fill="${P.bg}" rx="8"/>
  ${titleBar(W, 'Code Review: What It Catches vs. What It Misses')}
  <line x1="${W / 2}" y1="52" x2="${W / 2}" y2="${H - 12}" stroke="${P.border}" stroke-width="1.5"/>
  <rect x="${lX}" y="56" width="${COL_W}" height="28" fill="${P.greenDim}" rx="4"/>
  <text x="${lX + COL_W / 2}" y="75" font-family="sans-serif" font-size="13" font-weight="700"
    fill="#86efac" text-anchor="middle">CATCHES WELL</text>
  <rect x="${rX}" y="56" width="${COL_W}" height="28" fill="${P.redDim}" rx="4"/>
  <text x="${rX + COL_W / 2}" y="75" font-family="sans-serif" font-size="13" font-weight="700"
    fill="#fca5a5" text-anchor="middle">STRUCTURAL BLIND SPOTS</text>
  ${makeRows(left, lX, P.greenBg, P.green, '#dcfce7', '#4ade80')}
  ${makeRightRows(right, rX)}
  <line x1="16" y1="${H - 20}" x2="${W - 16}" y2="${H - 20}" stroke="${P.dim}" stroke-width="0.75"/>
  <text x="${W / 2}" y="${H - 7}" font-family="sans-serif" font-size="11" fill="${P.muted}"
    text-anchor="middle">Review is optimized for additions. Bugs hide in deletions.</text>
</svg>`;

  return { svg, W: W * 2, H: H * 2 };
}

// ---------------------------------------------------------------------------
// DIAGRAM 2: Why Tests Miss Bugs  (560 × 260)
// The green build fallacy: three CI badges + the gap + production failure.
// Does NOT reproduce the GenerateInvoiceAsync diff.
// ---------------------------------------------------------------------------
function makeDiagram2() {
  const W = 560, H = 260;

  const badge = (x, y, w, label, bg, fill) =>
    `<rect x="${x}" y="${y}" width="${w}" height="28" fill="${bg}" rx="4"/>
  <text x="${x + w / 2}" y="${y + 19}" font-family="sans-serif" font-size="14" font-weight="600"
    fill="${fill}" text-anchor="middle">${label}</text>`;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W * 2}" height="${H * 2}" viewBox="0 0 ${W} ${H}">
  <rect width="${W}" height="${H}" fill="${P.bg}" rx="8"/>
  ${titleBar(W, 'The Green Build Fallacy')}

  <!-- CI zone (green) -->
  <rect x="8" y="52" width="${W - 16}" height="80" fill="${P.greenBg}" rx="4" opacity="0.8"/>
  <text x="${W / 2}" y="70" font-family="sans-serif" font-size="13" font-weight="700"
    fill="#4ade80" text-anchor="middle">CI PIPELINE</text>
  ${badge(36,  78, 148, '&#x2713; Build passed', '#166534', '#86efac')}
  ${badge(206, 78, 148, '&#x2713; 23 / 23 tests', '#166534', '#86efac')}
  ${badge(376, 78, 148, '&#x2713; No warnings', '#166534', '#86efac')}

  <!-- Arrow down -->
  <line x1="${W / 2}" y1="136" x2="${W / 2}" y2="146" stroke="${P.muted}" stroke-width="2"/>
  <polygon points="${W/2 - 6},146 ${W/2 + 6},146 ${W/2},154" fill="${P.muted}"/>

  <!-- Gap zone (amber) -->
  <rect x="8" y="158" width="${W - 16}" height="44" fill="${P.amberBg}" rx="4" opacity="0.9"/>
  <text x="32" y="178" font-family="sans-serif" font-size="20" fill="${P.amber}">&#x26A0;</text>
  <text x="60" y="174" font-family="sans-serif" font-size="15" fill="#fde68a" font-weight="600">Guard removed</text>
  <text x="60" y="193" font-family="sans-serif" font-size="12" fill="#fbbf24">no existing test covered this path</text>

  <!-- Arrow down -->
  <line x1="${W / 2}" y1="206" x2="${W / 2}" y2="216" stroke="${P.muted}" stroke-width="2"/>
  <polygon points="${W/2 - 6},216 ${W/2 + 6},216 ${W/2},224" fill="${P.muted}"/>

  <!-- Production zone (red) -->
  <rect x="8" y="228" width="${W - 16}" height="24" fill="${P.redBg}" rx="4" opacity="0.9"/>
  <text x="${W / 2}" y="244" font-family="sans-serif" font-size="13" font-weight="700"
    fill="#f87171" text-anchor="middle">&#x2717; PRODUCTION  &#x2014;  NullReferenceException</text>
</svg>`;

  return { svg, W: W * 2, H: H * 2 };
}

// ---------------------------------------------------------------------------
// DIAGRAM 3: What Is Diff-Based Analysis  (560 × 220)
// Big-number comparison: 47 findings (full scan) vs 1 finding (diff scan).
// Visual contrast communicates the scope argument without reproducing code.
// ---------------------------------------------------------------------------
function makeDiagram3() {
  const W = 560, H = 220;
  const MID = W / 2;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W * 2}" height="${H * 2}" viewBox="0 0 ${W} ${H}">
  <rect width="${W}" height="${H}" fill="${P.bg}" rx="8"/>
  ${titleBar(W, 'Scope Determines Signal')}

  <!-- Divider -->
  <line x1="${MID}" y1="52" x2="${MID}" y2="${H - 20}" stroke="${P.border}" stroke-width="1.5"/>

  <!-- Left: full scan -->
  <rect x="8" y="56" width="${MID - 16}" height="28" fill="#1e3a5f" rx="4"/>
  <text x="${MID / 2 + 4}" y="75" font-family="sans-serif" font-size="13" font-weight="700"
    fill="${P.muted}" text-anchor="middle">FULL SCAN</text>

  <text x="${MID / 2 + 4}" y="138" font-family="sans-serif" font-size="72" font-weight="900"
    fill="${P.dim}" text-anchor="middle">47</text>
  <text x="${MID / 2 + 4}" y="160" font-family="sans-serif" font-size="16"
    fill="${P.muted}" text-anchor="middle">findings</text>
  <text x="${MID / 2 + 4}" y="178" font-family="sans-serif" font-size="13"
    fill="#475569" text-anchor="middle">498 lines scanned</text>

  <!-- Right: diff scan -->
  <rect x="${MID + 8}" y="56" width="${MID - 16}" height="28" fill="#0c2e40" rx="4"/>
  <text x="${MID + MID / 2 - 4}" y="75" font-family="sans-serif" font-size="13" font-weight="700"
    fill="${P.cyan}" text-anchor="middle">DIFF ONLY</text>

  <text x="${MID + MID / 2 - 4}" y="138" font-family="sans-serif" font-size="72" font-weight="900"
    fill="${P.cyan}" text-anchor="middle">1</text>
  <text x="${MID + MID / 2 - 4}" y="160" font-family="sans-serif" font-size="16"
    fill="${P.cyanDim}" text-anchor="middle">finding</text>
  <text x="${MID + MID / 2 - 4}" y="178" font-family="sans-serif" font-size="13"
    fill="#0e7490" text-anchor="middle">6 changed lines</text>

  <line x1="16" y1="${H - 20}" x2="${W - 16}" y2="${H - 20}" stroke="${P.dim}" stroke-width="0.75"/>
  <text x="${W / 2}" y="${H - 6}" font-family="sans-serif" font-size="11" fill="${P.muted}"
    text-anchor="middle">Same codebase. Narrower scope eliminates noise.</text>
</svg>`;

  return { svg, W: W * 2, H: H * 2 };
}

// ---------------------------------------------------------------------------
// DIAGRAM 4: Detect Breaking Changes Before Merge  (560 × 240)
// Two zones: what the compiler sees vs. what only the runtime sees.
// Does NOT reproduce the pipeline node diagram -- uses two-zone layout.
// ---------------------------------------------------------------------------
function makeDiagram4() {
  const W = 560, H = 240;
  const BOX_W = 248, BOX_H = 148, BOX_Y = 60;
  const lX = 8, rX = W - 8 - BOX_W;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W * 2}" height="${H * 2}" viewBox="0 0 ${W} ${H}">
  <rect width="${W}" height="${H}" fill="${P.bg}" rx="8"/>
  ${titleBar(W, "The Compiler's Blind Spot")}

  <!-- Left box: compile time (green tint) -->
  <rect x="${lX}" y="${BOX_Y}" width="${BOX_W}" height="${BOX_H}" fill="${P.greenBg}" stroke="${P.greenDim}" stroke-width="1" rx="6"/>
  <rect x="${lX}" y="${BOX_Y}" width="${BOX_W}" height="26" fill="${P.greenDim}" rx="6"/>
  <rect x="${lX}" y="${BOX_Y + 14}" width="${BOX_W}" height="12" fill="${P.greenDim}"/>
  <text x="${lX + BOX_W / 2}" y="${BOX_Y + 18}" font-family="sans-serif" font-size="12" font-weight="700"
    fill="#86efac" text-anchor="middle">COMPILER SEES</text>
  <text x="${lX + BOX_W / 2}" y="${BOX_Y + 60}" font-family="sans-serif" font-size="16"
    fill="#dcfce7" text-anchor="middle">Your source code</text>
  <text x="${lX + BOX_W / 2}" y="${BOX_Y + 82}" font-family="sans-serif" font-size="20"
    fill="${P.muted}" text-anchor="middle">&#x2193;</text>
  <text x="${lX + BOX_W / 2}" y="${BOX_Y + 105}" font-family="sans-serif" font-size="16"
    fill="#dcfce7" text-anchor="middle">Compiler</text>
  <rect x="${lX + 24}" y="${BOX_Y + 114}" width="${BOX_W - 48}" height="24" fill="${P.greenDim}" rx="3"/>
  <text x="${lX + BOX_W / 2}" y="${BOX_Y + 131}" font-family="sans-serif" font-size="14" font-weight="700"
    fill="${P.green}" text-anchor="middle">&#x2713; Builds clean</text>

  <!-- Arrow between boxes -->
  <text x="${W / 2}" y="${BOX_Y + BOX_H / 2 + 8}" font-family="sans-serif" font-size="13"
    fill="${P.muted}" text-anchor="middle">&#x2192; deploy &#x2192;</text>

  <!-- Right box: runtime (red tint) -->
  <rect x="${rX}" y="${BOX_Y}" width="${BOX_W}" height="${BOX_H}" fill="${P.redBg}" stroke="${P.redDim}" stroke-width="1" rx="6"/>
  <rect x="${rX}" y="${BOX_Y}" width="${BOX_W}" height="26" fill="${P.redDim}" rx="6"/>
  <rect x="${rX}" y="${BOX_Y + 14}" width="${BOX_W}" height="12" fill="${P.redDim}"/>
  <text x="${rX + BOX_W / 2}" y="${BOX_Y + 18}" font-family="sans-serif" font-size="12" font-weight="700"
    fill="#fca5a5" text-anchor="middle">COMPILER CANNOT SEE</text>
  <text x="${rX + BOX_W / 2}" y="${BOX_Y + 60}" font-family="sans-serif" font-size="16"
    fill="#fee2e2" text-anchor="middle">Consumer binary</text>
  <text x="${rX + BOX_W / 2}" y="${BOX_Y + 82}" font-family="sans-serif" font-size="20"
    fill="${P.muted}" text-anchor="middle">&#x2193;</text>
  <text x="${rX + BOX_W / 2}" y="${BOX_Y + 105}" font-family="sans-serif" font-size="16"
    fill="#fee2e2" text-anchor="middle">Calls removed method</text>
  <rect x="${rX + 24}" y="${BOX_Y + 114}" width="${BOX_W - 48}" height="24" fill="${P.redDim}" rx="3"/>
  <text x="${rX + BOX_W / 2}" y="${BOX_Y + 131}" font-family="sans-serif" font-size="13" font-weight="700"
    fill="${P.red}" text-anchor="middle">&#x2717; MissingMethodException</text>

  <!-- Footer -->
  <line x1="16" y1="${H - 20}" x2="${W - 16}" y2="${H - 20}" stroke="${P.dim}" stroke-width="0.75"/>
  <text x="${W / 2}" y="${H - 6}" font-family="sans-serif" font-size="11" fill="${P.muted}"
    text-anchor="middle">Compiler checks what it can see. Runtime checks everything.</text>
</svg>`;

  return { svg, W: W * 2, H: H * 2 };
}

// ---------------------------------------------------------------------------
const jobs = [
  { fn: makeDiagram1, file: 'why-code-review-misses-bugs-hero.png' },
  { fn: makeDiagram2, file: 'why-tests-miss-bugs-hero.png' },
  { fn: makeDiagram3, file: 'what-is-diff-based-analysis-hero.png' },
  { fn: makeDiagram4, file: 'detect-breaking-changes-before-merge-hero.png' },
];

for (const { fn, file } of jobs) {
  const { svg, W, H } = fn();
  const outPath = join(__dirname, '../public/articles', file);
  await sharp(Buffer.from(svg)).png().toFile(outPath);
  console.log(`ok  ${file}  (${W}x${H})`);
}

