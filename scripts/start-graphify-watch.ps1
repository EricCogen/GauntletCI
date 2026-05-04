# GauntletCI Graphify Watch Mode Startup Script
# Automatically detects changed files and rebuilds graph incrementally
# Runs in background while you develop

param(
    [int]$Debounce = 3,
    [string]$Path = "."
)

Write-Host "🔍 Starting Graphify Watch Mode" -ForegroundColor Cyan
Write-Host "   Watching: $Path" -ForegroundColor Gray
Write-Host "   Debounce: ${Debounce}s (waits for file activity to stop)" -ForegroundColor Gray
Write-Host ""
Write-Host "📊 Graph updates:" -ForegroundColor Cyan
Write-Host "   - Code-only changes: AST extraction only (fast, no LLM)" -ForegroundColor Gray
Write-Host "   - Docs/papers/images: marks --update needed (semantic re-extraction)" -ForegroundColor Gray
Write-Host ""
Write-Host "🛑 To stop watch mode:" -ForegroundColor Yellow
Write-Host "   - Windows Task Manager: Find python.exe, End Task" -ForegroundColor Gray
Write-Host "   - Or press Ctrl+C in the terminal that started it" -ForegroundColor Gray
Write-Host ""

# Ensure watchdog is installed
try {
    python3 -c "import watchdog" 2>&1 | Out-Null
} catch {
    Write-Host "📦 Installing watchdog dependency..." -ForegroundColor Cyan
    python3 -m pip install watchdog -q --no-warn-script-location
}

# Start watch mode
Write-Host "▶️  Watch mode starting..." -ForegroundColor Green
python3 -m graphify.watch $Path --debounce $Debounce

Write-Host ""
Write-Host "⏸️  Watch mode stopped" -ForegroundColor Yellow
