// SPDX-License-Identifier: Elastic-2.0
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// SQLite-backed vector store for Expert Reality Check embeddings.
/// Stores embedding vectors as BLOBs and performs cosine-similarity search in .NET.
/// Suitable for corpora up to ~100k entries; for larger sets, replace with an ANN index.
/// </summary>
public sealed class VectorStore : IDisposable
{
    private readonly SqliteConnection _db;

    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS expert_embeddings (
            id          TEXT PRIMARY KEY,
            content     TEXT NOT NULL,
            source      TEXT NOT NULL DEFAULT '',
            embedding   BLOB NOT NULL,
            dim         INTEGER NOT NULL,
            created_at  TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    /// <summary>Opens (or creates) the SQLite database at <paramref name="dbPath"/> and initializes the schema.</summary>
    /// <param name="dbPath">Filesystem path to the SQLite database file.</param>
    public VectorStore(string dbPath)
    {
        _db = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        _db.Open();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = Ddl;
        cmd.ExecuteNonQuery();
    }

    /// <summary>Disposes the underlying <see cref="SqliteConnection"/>.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>Upserts an embedding record. id is caller-supplied (e.g. "dotnet/runtime#12345").</summary>
    public void Upsert(string id, string content, string source, float[] embedding)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO expert_embeddings (id, content, source, embedding, dim, created_at)
            VALUES ($id, $content, $source, $blob, $dim, datetime('now'))
            ON CONFLICT(id) DO UPDATE SET
                content    = excluded.content,
                source     = excluded.source,
                embedding  = excluded.embedding,
                dim        = excluded.dim,
                created_at = excluded.created_at;
            """;
        cmd.Parameters.AddWithValue("$id",      id);
        cmd.Parameters.AddWithValue("$content", content);
        cmd.Parameters.AddWithValue("$source",  source);
        cmd.Parameters.AddWithValue("$blob",    FloatsToBytes(embedding));
        cmd.Parameters.AddWithValue("$dim",     embedding.Length);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Returns the top-K entries by cosine similarity to the query embedding.
    /// Loads all vectors into memory and scores them in .NET.
    /// </summary>
    public IReadOnlyList<VectorSearchResult> Search(float[] queryEmbedding, int topK = 5)
    {
        if (queryEmbedding.Length == 0) return [];

        var rows = new List<(string id, string content, string source, float[] vec)>();

        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "SELECT id, content, source, embedding FROM expert_embeddings WHERE dim = $dim";
            cmd.Parameters.AddWithValue("$dim", queryEmbedding.Length);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var blob = (byte[])reader["embedding"];
                rows.Add((
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    BytesToFloats(blob)
                ));
            }
        }

        return rows
            .Select(r => new VectorSearchResult(r.id, r.content, r.source, CosineSimilarity(queryEmbedding, r.vec)))
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    /// <summary>Returns the total number of embedding records currently stored in the database.</summary>
    public int Count()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM expert_embeddings";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ── Math ──────────────────────────────────────────────────────────────────

    /// <summary>Computes the cosine similarity between two equal-length vectors, clamped to [-1, 1].</summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        float dot = 0f, magA = 0f, magB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        // Cosine similarity = dot(a,b) / (|a| * |b|)
        // Clamp to [-1, 1] to handle floating-point precision errors
        var denom = MathF.Sqrt(magA) * MathF.Sqrt(magB);
        return denom == 0f ? 0f : dot / denom;
    }

    // ── Serialisation ─────────────────────────────────────────────────────────

    /// <summary>Serializes a float array to a raw byte array for SQLite BLOB storage.</summary>
    internal static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        MemoryMarshal.Cast<float, byte>(floats).CopyTo(bytes);
        return bytes;
    }

    /// <summary>Deserializes a raw byte array read from SQLite back into a float vector.</summary>
    internal static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        MemoryMarshal.Cast<byte, float>(bytes).CopyTo(floats);
        return floats;
    }
}

/// <summary>A single result entry returned by <see cref="VectorStore.Search"/>.</summary>
public sealed record VectorSearchResult(string Id, string Content, string Source, float Score);
