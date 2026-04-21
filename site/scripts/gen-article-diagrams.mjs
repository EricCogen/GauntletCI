/**
 * gen-article-diagrams.mjs
 * Generates programmatic SVG->PNG diagrams for all 4 articles.
 * Each diagram uses the article's own code content to make a specific point.
 * Run with: npm run gen-diagrams
 */
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const sharp = (await import('sharp')).default;

function esc(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

const C = {
  bg: '#0d1117',
  chrome: '#161b22',
  border: '#30363d',
  delBg: '#2a1215',
  addBg: '#12261e',
  del: '#f85149',
  add: '#3fb950',
  ctx: '#8b949e',
  meta: '#484f58',
  white: '#e6edf3',
  cyan: '#06b6d4',
  amber: '#d29922',
};

function windowChrome(w, h, title) {
  return `
  <rect width="${w}" height="${h}" fill="${C.bg}" rx="6"/>
  <rect width="${w}" height="24" fill="${C.chrome}" rx="6"/>
  <rect y="18" width="${w}" height="6" fill="${C.chrome}"/>
  <circle cx="13" cy="12" r="4" fill="#ff5f57"/>
  <circle cx="25" cy="12" r="4" fill="#febc2e"/>
  <circle cx="37" cy="12" r="4" fill="#28c840"/>
  <text x="${w / 2}" y="16" font-family="sans-serif" font-size="9" fill="${C.meta}" text-anchor="middle">${esc(title)}</text>
  <line x1="0" y1="24" x2="${w}" y2="24" stroke="${C.border}" stroke-width="1"/>
  <rect x="0" y="0" width="3" height="${h}" fill="${C.cyan}" rx="3"/>`;
}

// ---------------------------------------------------------------------------
// DIAGRAM 1: Why Code Review Misses Bugs
// UserService.cs diff: 5 removed guards vs 1 green addition + GCI badges.
// The visual ratio of red-to-green IS the argument.
// ---------------------------------------------------------------------------
function makeDiagram1() {
  const W = 540;
  const CHROME_H = 24;
  const ROW_H = 17;
  const PAD = 12;
  const CODE_W = 380;
  const ANNOT_X = CODE_W + 10;
  const ANNOT_W = W - ANNOT_X - 8;

  const lines = [
    { type: 'meta', text: '@@ -38,14 +38,9 @@' },
    { type: 'ctx',  text: '  public async Task<UserDto> GetUserAsync(...) {' },
    { type: 'del',  text: '-   if (id <= 0)' },
    { type: 'del',  text: '-     throw new ArgumentOutOfRangeException(nameof(id));' },
    { type: 'del',  text: '-   if (ct.IsCancellationRequested)' },
    { type: 'del',  text: '-     ct.ThrowIfCancellationRequested();' },
    { type: 'ctx',  text: '    var entity = await _repository.FindByIdAsync(...);' },
    { type: 'del',  text: '-   if (entity == null)' },
    { type: 'del',  text: '-     throw new NotFoundException("User not found.");' },
    { type: 'add',  text: '+   ArgumentNullException.ThrowIfNull(entity);' },
    { type: 'ctx',  text: '    return _mapper.Map<UserDto>(entity);' },
  ];

  const codeStartY = CHROME_H + 6;
  const codeEndY = codeStartY + lines.length * ROW_H;
  const FIND_Y = codeEndY + 8;
  const H = FIND_Y + 38 + 10;

  const rowsSvg = lines.map((line, i) => {
    const y = codeStartY + i * ROW_H;
    const textY = y + ROW_H - 4;
    let bg = '';
    let fill = line.type === 'meta' ? C.meta : C.ctx;
    if (line.type === 'del') {
      bg = `<rect x="${PAD}" y="${y}" width="${CODE_W - PAD}" height="${ROW_H}" fill="${C.delBg}"/>`;
      fill = C.del;
    } else if (line.type === 'add') {
      bg = `<rect x="${PAD}" y="${y}" width="${CODE_W - PAD}" height="${ROW_H}" fill="${C.addBg}"/>`;
      fill = C.add;
    }
    return `${bg}<text x="${PAD + 3}" y="${textY}" font-family="monospace" font-size="9.5" fill="${fill}">${esc(line.text)}</text>`;
  }).join('\n  ');

  // Annotation brackets — red block spans rows 2-5 and 7-8 (the 5 deletions)
  const redY1 = codeStartY + 2 * ROW_H;
  const redY2 = codeStartY + 9 * ROW_H;
  const greenY = codeStartY + 9 * ROW_H;

  const annotSvg = `
  <rect x="${ANNOT_X}" y="${redY1}" width="${ANNOT_W}" height="${redY2 - redY1}"
    fill="rgba(248,81,73,0.06)" stroke="rgba(248,81,73,0.4)" stroke-width="0.75"
    stroke-dasharray="3,2" rx="3"/>
  <text x="${ANNOT_X + 4}" y="${redY1 + 13}" font-family="sans-serif" font-size="8.5"
    fill="${C.del}" font-weight="600">5 guards</text>
  <text x="${ANNOT_X + 4}" y="${redY1 + 24}" font-family="sans-serif" font-size="8.5"
    fill="${C.del}">removed</text>
  <rect x="${ANNOT_X}" y="${greenY}" width="${ANNOT_W}" height="${ROW_H}"
    fill="rgba(63,185,80,0.06)" stroke="rgba(63,185,80,0.4)" stroke-width="0.75" rx="3"/>
  <text x="${ANNOT_X + 4}" y="${greenY + ROW_H - 4}" font-family="sans-serif" font-size="8"
    fill="${C.add}">1 addition</text>`;

  const findSvg = `
  <line x1="${PAD}" y1="${FIND_Y}" x2="${W - 8}" y2="${FIND_Y}" stroke="${C.border}" stroke-width="0.75"/>
  <rect x="${PAD}" y="${FIND_Y + 5}" width="46" height="13" fill="rgba(248,81,73,0.15)" rx="2"/>
  <text x="${PAD + 3}" y="${FIND_Y + 15}" font-family="monospace" font-size="8"
    fill="${C.del}" font-weight="600">GCI0001</text>
  <text x="${PAD + 52}" y="${FIND_Y + 15}" font-family="sans-serif" font-size="8.5"
    fill="${C.ctx}">guard removed -- negative IDs reach database</text>
  <rect x="${PAD}" y="${FIND_Y + 22}" width="46" height="13" fill="rgba(210,153,34,0.15)" rx="2"/>
  <text x="${PAD + 3}" y="${FIND_Y + 32}" font-family="monospace" font-size="8"
    fill="${C.amber}" font-weight="600">GCI0014</text>
  <text x="${PAD + 52}" y="${FIND_Y + 32}" font-family="sans-serif" font-size="8.5"
    fill="${C.ctx}">exception type changed -- callers miss this path</text>`;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
  ${windowChrome(W, H, 'UserService.cs -- staged diff')}
  ${rowsSvg}
  ${annotSvg}
  ${findSvg}
</svg>`;

  return { svg, W, H };
}

// ---------------------------------------------------------------------------
// DIAGRAM 2: Why Tests Miss Bugs
// GenerateInvoiceAsync: guard removed during refactor.
// Two badges side-by-side show the paradox: CI green, GauntletCI red.
// ---------------------------------------------------------------------------
function makeDiagram2() {
  const W = 440;
  const CHROME_H = 24;
  const ROW_H = 17;
  const PAD = 12;

  const lines = [
    { type: 'meta', text: '@@ -2,8 +2,5 @@' },
    { type: 'ctx',  text: '  public async Task<InvoiceResult> GenerateInvoiceAsync(...)' },
    { type: 'ctx',  text: '  {' },
    { type: 'del',  text: '-     if (order.Items.Count == 0)' },
    { type: 'del',  text: '-         return InvoiceResult.Empty; // guard: skip empty orders' },
    { type: 'ctx',  text: '      var invoice = await _invoiceService.CreateAsync(order);' },
    { type: 'ctx',  text: '      // ... email, audit, return' },
  ];

  const codeStartY = CHROME_H + 6;
  const codeEndY = codeStartY + lines.length * ROW_H;
  const BADGE_Y = codeEndY + 8;
  const H = BADGE_Y + 38 + 10;

  const rowsSvg = lines.map((line, i) => {
    const y = codeStartY + i * ROW_H;
    const textY = y + ROW_H - 4;
    let bg = '';
    let fill = line.type === 'meta' ? C.meta : C.ctx;
    if (line.type === 'del') {
      bg = `<rect x="${PAD}" y="${y}" width="${W - PAD - 8}" height="${ROW_H}" fill="${C.delBg}"/>`;
      fill = C.del;
    }
    return `${bg}<text x="${PAD + 3}" y="${textY}" font-family="monospace" font-size="8.5" fill="${fill}">${esc(line.text)}</text>`;
  }).join('\n  ');

  // Two contrasting badges — the whole paradox in 2 lines
  const badgeSvg = `
  <line x1="${PAD}" y1="${BADGE_Y}" x2="${W - PAD}" y2="${BADGE_Y}" stroke="${C.border}" stroke-width="0.75"/>
  <rect x="${PAD}" y="${BADGE_Y + 5}" width="190" height="14"
    fill="rgba(63,185,80,0.12)" stroke="rgba(63,185,80,0.3)" stroke-width="0.75" rx="3"/>
  <text x="${PAD + 6}" y="${BADGE_Y + 16}" font-family="sans-serif" font-size="9"
    fill="${C.add}" font-weight="600">+ CI pipeline: 23 tests passed</text>
  <rect x="${PAD}" y="${BADGE_Y + 22}" width="240" height="14"
    fill="rgba(248,81,73,0.12)" stroke="rgba(248,81,73,0.3)" stroke-width="0.75" rx="3"/>
  <text x="${PAD + 6}" y="${BADGE_Y + 33}" font-family="sans-serif" font-size="9"
    fill="${C.del}" font-weight="600">x GCI0010: guard removed -- empty orders reach DB</text>`;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
  ${windowChrome(W, H, 'GenerateInvoiceAsync -- staged diff')}
  ${rowsSvg}
  ${badgeSvg}
</svg>`;

  return { svg, W, H };
}

// ---------------------------------------------------------------------------
// DIAGRAM 3: What is Diff-Based Analysis
// Two-panel: full file (wall of undifferentiated lines, 47 findings)
//         vs diff only (single highlighted deletion, 1 finding).
// The ratio tells the story: broad scope = noise; diff scope = signal.
// ---------------------------------------------------------------------------
function makeDiagram3() {
  const W = 480;
  const H = 280;
  const CHROME_H = 24;
  const MID = W / 2;
  const PAD = 10;
  const PANEL_W = MID - PAD * 1.5;
  const contentY = CHROME_H + 16; // extra space for panel labels
  const BADGE_H = 26;
  const contentH = H - contentY - BADGE_H - 4;
  const LINE_H = 13;
  const lineCount = Math.floor(contentH / LINE_H);
  const rightX = MID + PAD / 2;

  // Deterministic pseudo-random line widths
  const lineWidths = Array.from({ length: lineCount }, (_, i) => 30 + (i * 23 % 60));

  // Left panel: all lines equally lit (undifferentiated noise)
  const leftLines = lineWidths.map((w, i) => {
    const y = contentY + i * LINE_H;
    return `<rect x="${PAD + 14}" y="${y + 2}" width="${w}" height="8" fill="#21262d" rx="2"/>`;
  }).join('\n  ');

  // Right panel: diff view — only the delta is visible
  const rightLines = lineWidths.map((w, i) => {
    const y = contentY + i * LINE_H;
    if (i === 4) {
      // The one deleted line — bright red, full row highlight
      return [
        `<rect x="${rightX}" y="${y}" width="${PANEL_W + 4}" height="${LINE_H}" fill="${C.delBg}"/>`,
        `<rect x="${rightX + 14}" y="${y + 2}" width="${w}" height="8" fill="${C.del}" rx="2"/>`,
      ].join('\n  ');
    }
    if (i >= 3 && i <= 5) {
      // Context lines around the change — slightly visible
      return `<rect x="${rightX + 14}" y="${y + 2}" width="${w}" height="8" fill="#1c2128" rx="2"/>`;
    }
    // Everything else — dark, below noise threshold
    return `<rect x="${rightX + 14}" y="${y + 2}" width="${w}" height="8" fill="#0d1117" rx="2"/>`;
  }).join('\n  ');

  const BADGE_Y = H - BADGE_H - 2;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
  <rect width="${W}" height="${H}" fill="${C.bg}" rx="6"/>
  <rect width="${W}" height="${CHROME_H}" fill="${C.chrome}" rx="6"/>
  <rect y="18" width="${W}" height="6" fill="${C.chrome}"/>
  <circle cx="13" cy="12" r="4" fill="#ff5f57"/>
  <circle cx="25" cy="12" r="4" fill="#febc2e"/>
  <circle cx="37" cy="12" r="4" fill="#28c840"/>
  <rect x="0" y="0" width="3" height="${H}" fill="${C.cyan}" rx="3"/>
  <line x1="0" y1="${CHROME_H}" x2="${W}" y2="${CHROME_H}" stroke="${C.border}" stroke-width="1"/>

  <!-- Panel labels -->
  <text x="${PAD + PANEL_W / 2 + 7}" y="${CHROME_H + 11}" font-family="sans-serif" font-size="8.5"
    fill="${C.meta}" text-anchor="middle" font-weight="600">FULL FILE</text>
  <text x="${rightX + PANEL_W / 2}" y="${CHROME_H + 11}" font-family="sans-serif" font-size="8.5"
    fill="${C.cyan}" text-anchor="middle" font-weight="600">DIFF ONLY</text>

  <!-- Panel divider -->
  <line x1="${MID}" y1="${CHROME_H}" x2="${MID}" y2="${H}" stroke="${C.border}" stroke-width="0.75"/>

  ${leftLines}
  ${rightLines}

  <!-- Bottom badges -->
  <line x1="0" y1="${BADGE_Y}" x2="${W}" y2="${BADGE_Y}" stroke="${C.border}" stroke-width="0.75"/>
  <rect x="${PAD}" y="${BADGE_Y + 4}" width="${PANEL_W}" height="18"
    fill="rgba(139,148,158,0.08)" rx="3"/>
  <text x="${PAD + PANEL_W / 2}" y="${BADGE_Y + 16}" font-family="sans-serif" font-size="9"
    fill="${C.ctx}" text-anchor="middle">498 lines scanned -- 47 findings</text>
  <rect x="${rightX}" y="${BADGE_Y + 4}" width="${PANEL_W - 2}" height="18"
    fill="rgba(248,81,73,0.1)" stroke="rgba(248,81,73,0.3)" stroke-width="0.75" rx="3"/>
  <text x="${rightX + (PANEL_W - 2) / 2}" y="${BADGE_Y + 16}" font-family="sans-serif" font-size="9"
    fill="${C.del}" text-anchor="middle" font-weight="600">6 lines -- 1 finding</text>
</svg>`;

  return { svg, W, H };
}

// ---------------------------------------------------------------------------
// DIAGRAM 4: Detect Breaking Changes Before Merge
// Pipeline: change -> compiler(ok) -> tests(ok) -> deploy -> runtime(fail)
// Bracket annotations show what each stage actually checks.
// No GauntletCI mention -- the gap speaks for itself.
// ---------------------------------------------------------------------------
function makeDiagram4() {
  const W = 520;
  const H = 210;
  const NODE_Y = 72;
  const NODE_W = 76;
  const NODE_H = 44;

  const nodes = [
    { id: 'change',   label: 'change',   sub: 'PR submitted', color: C.ctx,   x: 44 },
    { id: 'compiler', label: 'compiler', sub: 'build ok',     color: C.add,   x: 148 },
    { id: 'tests',    label: 'tests',    sub: '31 passed',    color: C.add,   x: 252 },
    { id: 'deploy',   label: 'deploy',   sub: 'pushed',       color: C.amber, x: 356 },
    { id: 'runtime',  label: 'runtime',  sub: 'MissingMethodException', color: C.del, x: 460 },
  ];

  function nodeBg(color) {
    if (color === C.del) return 'rgba(248,81,73,0.12)';
    if (color === C.add) return 'rgba(63,185,80,0.12)';
    if (color === C.amber) return 'rgba(210,153,34,0.12)';
    return 'rgba(139,148,158,0.08)';
  }
  function nodeBorder(color) {
    if (color === C.del) return 'rgba(248,81,73,0.5)';
    if (color === C.add) return 'rgba(63,185,80,0.5)';
    if (color === C.amber) return 'rgba(210,153,34,0.4)';
    return C.border;
  }
  function nodeIcon(color) {
    if (color === C.add) return ' ok';
    if (color === C.del) return ' fail';
    return '';
  }

  const nodeSvg = nodes.map(n => `
  <rect x="${n.x - NODE_W / 2}" y="${NODE_Y}" width="${NODE_W}" height="${NODE_H}"
    fill="${nodeBg(n.color)}" stroke="${nodeBorder(n.color)}" stroke-width="1" rx="4"/>
  <text x="${n.x}" y="${NODE_Y + 17}" font-family="sans-serif" font-size="9.5"
    fill="${n.color}" text-anchor="middle" font-weight="700">${esc(n.label + nodeIcon(n.color))}</text>
  <text x="${n.x}" y="${NODE_Y + 32}" font-family="monospace" font-size="7.5"
    fill="${C.meta}" text-anchor="middle">${esc(n.sub)}</text>`).join('');

  // Connector arrows
  const arrowSvg = nodes.slice(0, -1).map((n, i) => {
    const x1 = n.x + NODE_W / 2;
    const x2 = nodes[i + 1].x - NODE_W / 2;
    const midY = NODE_Y + NODE_H / 2;
    return `<line x1="${x1}" y1="${midY}" x2="${x2 - 2}" y2="${midY}" stroke="${C.border}" stroke-width="1.5"/>
  <polygon points="${x2 - 2},${midY - 3} ${x2 + 4},${midY} ${x2 - 2},${midY + 3}" fill="${C.border}"/>`;
  }).join('\n  ');

  // Bracket: "checks source" under compiler+tests
  const bracketLeft = nodes[1].x - NODE_W / 2;
  const bracketRight = nodes[2].x + NODE_W / 2;
  const bracketY = NODE_Y + NODE_H + 10;

  // Bracket: "checks binaries" under runtime
  const rBracketLeft = nodes[4].x - NODE_W / 2;
  const rBracketRight = nodes[4].x + NODE_W / 2 - 2;
  const rBracketY = bracketY;

  const annotSvg = `
  <line x1="${bracketLeft}" y1="${bracketY}" x2="${bracketRight}" y2="${bracketY}" stroke="${C.add}" stroke-width="1"/>
  <line x1="${bracketLeft}" y1="${bracketY}" x2="${bracketLeft}" y2="${bracketY - 4}" stroke="${C.add}" stroke-width="1"/>
  <line x1="${bracketRight}" y1="${bracketY}" x2="${bracketRight}" y2="${bracketY - 4}" stroke="${C.add}" stroke-width="1"/>
  <text x="${(bracketLeft + bracketRight) / 2}" y="${bracketY + 11}" font-family="sans-serif"
    font-size="8" fill="${C.add}" text-anchor="middle">checks source</text>
  <line x1="${rBracketLeft}" y1="${rBracketY}" x2="${rBracketRight}" y2="${rBracketY}" stroke="${C.del}" stroke-width="1"/>
  <line x1="${rBracketLeft}" y1="${rBracketY}" x2="${rBracketLeft}" y2="${rBracketY - 4}" stroke="${C.del}" stroke-width="1"/>
  <line x1="${rBracketRight}" y1="${rBracketY}" x2="${rBracketRight}" y2="${rBracketY - 4}" stroke="${C.del}" stroke-width="1"/>
  <text x="${nodes[4].x}" y="${rBracketY + 11}" font-family="sans-serif"
    font-size="8" fill="${C.del}" text-anchor="middle">checks binaries</text>`;

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
  <rect width="${W}" height="${H}" fill="${C.bg}" rx="6"/>
  <rect x="0" y="0" width="3" height="${H}" fill="${C.cyan}" rx="3"/>

  <!-- Title -->
  <text x="${W / 2}" y="22" font-family="sans-serif" font-size="10.5"
    fill="${C.white}" text-anchor="middle" font-weight="600">The compile-to-runtime gap</text>
  <text x="${W / 2}" y="38" font-family="sans-serif" font-size="8.5"
    fill="${C.ctx}" text-anchor="middle">Compiler checks source. Runtime checks binaries. Different checks.</text>
  <line x1="16" y1="48" x2="${W - 16}" y2="48" stroke="${C.border}" stroke-width="0.75"/>

  ${arrowSvg}
  ${nodeSvg}
  ${annotSvg}
</svg>`;

  return { svg, W, H };
}

// ---------------------------------------------------------------------------
// Generate all 4
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
