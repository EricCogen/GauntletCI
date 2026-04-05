# GauntletCI

GauntletCI is a pre-commit developer tool that evaluates code changes with an LLM-driven audit before commits are finalized.

## Solution Layout

- `src/GauntletCI.Core` - Core evaluation engine library
- `src/GauntletCI.Cli` - Console application entry point
- `src/GauntletCI.CopilotExtension` - Extension-facing integration library scaffold
- `tests/GauntletCI.Core.Tests` - Unit tests for core logic
- `tests/GauntletCI.Cli.Tests` - Unit tests for CLI behavior

## Build and Test

```powershell
dotnet restore GauntletCI.slnx
dotnet build GauntletCI.slnx
dotnet test GauntletCI.slnx
```
