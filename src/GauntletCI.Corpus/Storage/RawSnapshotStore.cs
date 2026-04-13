// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Models;

namespace GauntletCI.Corpus.Storage;

/// <summary>
/// Persists raw API response snapshots into a fixture's raw/ subfolder.
/// This ensures original payloads are never lost, even if normalization changes.
/// </summary>
public sealed class RawSnapshotStore
{
    private readonly string _basePath;

    public RawSnapshotStore(string basePath = "./data/fixtures")
    {
        _basePath = basePath;
    }

    public async Task SaveAsync(
        FixtureTier tier,
        string fixtureId,
        string fileName,
        string content,
        CancellationToken ct = default)
    {
        var rawDir = FixtureIdHelper.GetRawPath(
            FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId));

        Directory.CreateDirectory(rawDir);
        await File.WriteAllTextAsync(Path.Combine(rawDir, fileName), content, ct);
    }

    public async Task<string?> LoadAsync(
        FixtureTier tier, string fixtureId, string fileName, CancellationToken ct = default)
    {
        var path = Path.Combine(
            FixtureIdHelper.GetRawPath(
                FixtureIdHelper.GetFixturePath(_basePath, tier, fixtureId)),
            fileName);

        return File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;
    }
}
