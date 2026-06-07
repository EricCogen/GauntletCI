// SPDX-License-Identifier: Elastic-2.0
using System.Text.RegularExpressions;

namespace GauntletCI.Core.Git;

/// <summary>
/// Validates git ref strings before passing them as subprocess arguments.
/// Rejects option-like values and characters outside the git ref alphabet.
/// </summary>
public static class GitRefValidator
{
    private static readonly Regex RefPartPattern = new(
        @"^[a-zA-Z0-9._~/^:@{}+-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="commitRef"/> is not a safe git ref or range.
    /// </summary>
    public static void ValidateRef(string commitRef)
    {
        if (string.IsNullOrWhiteSpace(commitRef))
            throw new ArgumentException("Commit reference must not be empty.", nameof(commitRef));

        if (commitRef.StartsWith('-'))
            throw new ArgumentException($"Invalid git ref: {commitRef}", nameof(commitRef));

        if (commitRef.Contains("..", StringComparison.Ordinal))
        {
            var parts = commitRef.Split("..", 2, StringSplitOptions.None);
            ValidateRefPart(parts[0]);
            ValidateRefPart(parts[1]);
            return;
        }

        ValidateRefPart(commitRef);
    }

    private static void ValidateRefPart(string part)
    {
        if (part.Length == 0)
            throw new ArgumentException("Invalid git ref range.", nameof(part));

        if (!RefPartPattern.IsMatch(part))
            throw new ArgumentException($"Invalid git ref: {part}", nameof(part));
    }
}
