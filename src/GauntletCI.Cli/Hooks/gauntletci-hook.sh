#!/bin/bash
# GauntletCI Pre-Commit Hook
# Installed by `gauntletci init`

set -e

resolve_gauntletci() {
    local current
    current="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
    while [ -n "$current" ] && [ "$current" != "/" ]; do
        if [ -f "$current/src/GauntletCI.Cli/GauntletCI.Cli.csproj" ]; then
            GAUNTLETCI_MODE="repo"
            GAUNTLETCI_REPO_ROOT="$current"
            return 0
        fi
        current="$(dirname "$current")"
    done

    if command -v gauntletci &> /dev/null; then
        GAUNTLETCI_MODE="shim"
        GAUNTLETCI_CMD="gauntletci"
        return 0
    fi

    local default_tools="$HOME/.dotnet/tools"
    local tools_dirs=()
    if [ -n "${DOTNET_ROOT:-}" ]; then
        tools_dirs+=("$DOTNET_ROOT/tools")
    fi
    tools_dirs+=("$default_tools")

    local dir
    for dir in "${tools_dirs[@]}"; do
        if [ -x "$dir/gauntletci" ]; then
            GAUNTLETCI_MODE="shim"
            GAUNTLETCI_CMD="$dir/gauntletci"
            return 0
        fi

        if [ -x "$dir/gauntletci.exe" ]; then
            GAUNTLETCI_MODE="shim"
            GAUNTLETCI_CMD="$dir/gauntletci.exe"
            return 0
        fi
    done

    return 1
}

run_gauntletci() {
    if [ "$GAUNTLETCI_MODE" = "repo" ]; then
        dotnet run --project "$GAUNTLETCI_REPO_ROOT/src/GauntletCI.Cli" -- "$@"
    else
        "$GAUNTLETCI_CMD" "$@"
    fi
}

print_findings_summary() {
    local output="$1"
    if [ $HAS_JQ -eq 1 ]; then
        local total
        total=$(echo "$output" | jq '.Findings | length')
        if [ "$total" -eq 0 ]; then
            echo "GauntletCI found no issues."
            return
        fi
        echo ""
        echo "GauntletCI found $total issue(s):"
        echo "$output" | jq -r '.Findings[] | "  • [\(.RuleId)] \(.Summary)"'
        local dropped
        dropped=$(echo "$output" | jq '((.Delivery.DroppedByGlobalCap // 0) + (.Delivery.DroppedByPerRuleCap // 0))')
        if [ "$dropped" -gt 0 ] 2>/dev/null; then
            echo ""
            echo "Note: $dropped finding(s) were dropped by delivery caps (see Delivery in JSON output)."
        fi
    else
        echo "$output"
    fi
}

GAUNTLETCI_MODE=""
GAUNTLETCI_CMD=""
GAUNTLETCI_REPO_ROOT=""
if ! resolve_gauntletci; then
    echo "GauntletCI not found. Install with: dotnet tool install -g GauntletCI"
    echo "Or from this repo: ./scripts/install-gauntletci-global-tool.ps1"
    if [ "${GAUNTLETCI_HOOK_OPTIONAL:-}" = "1" ]; then
        exit 0
    fi
    exit 1
fi

echo "GauntletCI: Analyzing staged changes..."

HAS_JQ=0
if command -v jq &> /dev/null; then
    HAS_JQ=1
fi

set +e
OUTPUT=$(run_gauntletci analyze --staged --output json --no-banner 2>&1)
EXIT_CODE=$?
set -e

if [ "$EXIT_CODE" -ne 0 ]; then
    echo ""
    echo "Commit aborted. GauntletCI exited with code $EXIT_CODE (see .gauntletci.json exitOn and sensitivity)."
    print_findings_summary "$OUTPUT"
    exit 1
fi

print_findings_summary "$OUTPUT"
