namespace GauntletCI.Core.Evaluation;

public sealed record ChangeBlock(string FilePath, IReadOnlyList<string> HunkLines);
