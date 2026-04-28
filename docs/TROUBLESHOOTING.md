# Troubleshooting

Common issues and their fixes.

---

## Installation

### `dotnet tool install -g GauntletCI` fails

**Symptom:** `error NU1301: Unable to load the service index`

**Fix:** Add the GitHub Packages source, or confirm you're on .NET 8 SDK or later:

```bash
dotnet --version   # must be 8.x or later
```

---

### `gauntletci: command not found` after install

**Symptom:** The tool installs but the shell can't find it.

**Fix:** The .NET global tools directory is not on your `PATH`. Add it:

```bash
# Linux/macOS
export PATH="$HOME/.dotnet/tools:$PATH"

# Windows (PowerShell)
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"
```

Add the export to your shell profile (`.bashrc`, `.zshrc`, or PowerShell `$PROFILE`) to make it permanent.

---

## Analysis

### `No diff source provided`

**Symptom:** Running `gauntletci analyze` with no flags produces this error.

**Fix:** Provide a diff source:

```bash
gauntletci analyze --staged          # analyze git staged changes
gauntletci analyze --commit HEAD~1   # analyze a specific commit
gauntletci analyze --diff pr.diff    # analyze a diff file
```

---

### Analysis produces 0 findings on a large PR

**Possible causes:**

1. **All files were filtered as ineligible**: GauntletCI skips binary files, generated code, vendored dependencies, and lock files. Run with `--verbose` to see the file eligibility report.
2. **Rules are disabled in config**: check `.gauntletci.json` for `"enabled": false` entries.
3. **Diff was empty**: `git diff --staged` with nothing staged produces an empty diff.

---

### Too many findings / noise

**Fix:** Tune severity thresholds in `.gauntletci.json`:

```json
{
  "rules": {
    "GCI0004": { "severity": "High" },
    "GCI0001": { "enabled": false }
  },
  "minSeverity": "High"
}
```

Use `gauntletci ignore add GCI00XX` to add a one-time suppression for a specific rule.

---

## LLM enrichment

### `--with-llm` produces empty enrichment text

**Symptom:** Findings show no enrichment text despite the flag being set.

**Causes and fixes:**

| Cause | Fix |
|-------|-----|
| Model not downloaded | Run `gauntletci model download` first |
| CPU inference timeout | Inference on CPU takes 30-90 seconds per finding. The default timeout is 60 seconds: this is usually sufficient, but a heavily loaded machine may need longer. |
| DirectML not available | The model falls back to CPU automatically. GPU via DirectML greatly speeds up inference on Windows. |

---

### `gauntletci model download` is slow or stalls

The Phi-4 Mini ONNX model is ~2.6 GB. Download speed depends on your connection. The model is cached at `~/.gauntletci/models/phi4-mini` after the first download.

If the download stalls, cancel and retry: the download is resumable.

---

### `--with-expert-context` returns no context

**Symptom:** Flag is set but no expert context appears in findings.

**Fix:** The vector store must be seeded first:

```bash
gauntletci llm seed
```

This writes 11 hand-curated .NET expert facts to `~/.gauntletci/expert-embeddings.db`. The Ollama embedding engine must be running:

```bash
ollama serve   # in a separate terminal
```

---

## Corpus pipeline

### `corpus discover` finds 0 candidates

**Possible causes:**

1. **No GitHub token configured**: set `GITHUB_TOKEN` environment variable or place a PAT in `.misc/ghapi.key`
2. **Rate limit hit**: the GitHub API has rate limits. Wait and retry, or use a token with higher limits.
3. **Network timeout**: retry with `--max-pages 1` to test connectivity.

---

### `corpus run-all` is slow

The corpus runner evaluates all rules against every fixture. With 500+ fixtures and 20 active rules, this can take 10-30 minutes. Run a subset:

```bash
gauntletci corpus run-all --limit 50
```

---

### Corpus DB is corrupted or needs reset

```bash
# Back up first
Copy-Item data/gauntletci-corpus.db data/gauntletci-corpus.db.bak

# Delete and reinitialize (data loss: be sure you have a backup)
Remove-Item data/gauntletci-corpus.db
gauntletci corpus discover   # recreates the DB with fresh schema
```

---

## Telemetry

### How do I opt out of telemetry?

```bash
gauntletci telemetry opt-out
```

This writes `"consent": "OptedOut"` to `~/.gauntletci/config.json`. No data is sent after opt-out.

### Where is telemetry stored locally?

`~/.gauntletci/telemetry-queue.json`: a bounded JSON queue (max 500 events). Inspect it at any time.

---

## GitHub Actions

### Action fails with `gauntletci: not found`

**Fix:** Ensure `dotnet-version` is set to `8.0.x` in the action input and the .NET tools path is on `PATH`. The composite action handles this automatically: verify you're using the action correctly:

```yaml
- uses: EricCogen/GauntletCI@v1
  with:
    commit: ${{ github.sha }}
```

### Exit code 1 blocks the PR

GauntletCI exits with code 1 when high-severity findings are present and `fail-on-findings: true` (the default). To make it advisory only:

```yaml
- uses: EricCogen/GauntletCI@v1
  with:
    fail-on-findings: false
```

---

## Still stuck?

Open an issue on GitHub with:
- GauntletCI version (`gauntletci --version`)
- OS and .NET SDK version (`dotnet --version`)
- The command you ran
- The full output (use `--verbose` if applicable)
