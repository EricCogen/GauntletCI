// SPDX-License-Identifier: Elastic-2.0
using System.CommandLine;
using System.Text.Json;
using GauntletCI.Core.Configuration;
using GauntletCI.Llm;
using GauntletCI.Llm.Embeddings;

namespace GauntletCI.Cli.Commands;

public static class LlmCommand
{
    private static readonly string DefaultVectorDb = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gauntletci", "expert-embeddings.db");

    public static Command Create()
    {
        var cmd = new Command("llm", "Expert knowledge distillation and embedding operations");
        cmd.AddCommand(CreateSeed());
        cmd.AddCommand(CreateDistill());
        return cmd;
    }

    // ── gauntletci llm seed ───────────────────────────────────────────────────

    private static Command CreateSeed()
    {
        var dbOpt    = new Option<string>("--db",              () => DefaultVectorDb, "Path to expert embeddings SQLite DB");
        var modelOpt = new Option<string>("--embedding-model", () => LlmDefaults.OllamaModel, "Ollama embedding model name");
        var urlOpt   = new Option<string>("--ollama-url",      () => "http://localhost:11434", "Ollama base URL");

        var cmd = new Command("seed", "Seed the expert vector store with hand-curated .NET expert facts");
        cmd.AddOption(dbOpt);
        cmd.AddOption(modelOpt);
        cmd.AddOption(urlOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var db    = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var model = ctx.ParseResult.GetValueForOption(modelOpt)!;
            var url   = ctx.ParseResult.GetValueForOption(urlOpt)!;
            var ct    = ctx.GetCancellationToken();

            Directory.CreateDirectory(Path.GetDirectoryName(db)!);

            Console.WriteLine($"[llm] Seeding {ExpertSeedFacts.All.Count} expert facts → {db}");

            using var store     = new VectorStore(db);
            using var embedding = new OllamaEmbeddingEngine(model, url);

            var distillery = new Distillery(new NullLlmEngine(), embedding, store);
            var seeded     = await distillery.SeedAsync(ExpertSeedFacts.All, ct);

            Console.WriteLine($"[llm] Seeded {seeded}/{ExpertSeedFacts.All.Count} facts (skipped {ExpertSeedFacts.All.Count - seeded}: embedding unavailable)");
        });

        return cmd;
    }

    // ── gauntletci llm distill ────────────────────────────────────────────────

    private static Command CreateDistill()
    {
        var inputOpt  = new Option<string>("--input",           () => "./data/maintainer-records.ndjson", "NDJSON file from 'corpus maintainers fetch'");
        var dbOpt     = new Option<string>("--db",              () => DefaultVectorDb, "Path to expert embeddings SQLite DB");
        var maxOpt    = new Option<int>   ("--max",             () => 50,              "Max records to distil (sorted by reactions)");
        var modelOpt  = new Option<string>("--ollama-model",    () => LlmDefaults.OllamaModel, "Ollama model for fact extraction");
        var embedOpt  = new Option<string>("--embedding-model", () => LlmDefaults.OllamaModel, "Ollama embedding model");
        var urlOpt    = new Option<string>("--ollama-url",      () => "http://localhost:11434", "Ollama base URL");

        var cmd = new Command("distill", "Extract expert facts from maintainer records and store as embeddings");
        cmd.AddOption(inputOpt);
        cmd.AddOption(dbOpt);
        cmd.AddOption(maxOpt);
        cmd.AddOption(modelOpt);
        cmd.AddOption(embedOpt);
        cmd.AddOption(urlOpt);

        cmd.SetHandler(async (ctx) =>
        {
            var input   = ctx.ParseResult.GetValueForOption(inputOpt)!;
            var db      = ctx.ParseResult.GetValueForOption(dbOpt)!;
            var max     = ctx.ParseResult.GetValueForOption(maxOpt);
            var model   = ctx.ParseResult.GetValueForOption(modelOpt)!;
            var embedM  = ctx.ParseResult.GetValueForOption(embedOpt)!;
            var url     = ctx.ParseResult.GetValueForOption(urlOpt)!;
            var ct      = ctx.GetCancellationToken();

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"[llm] Input file not found: {input}");
                ctx.ExitCode = 1;
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(db)!);

            var records = new List<DistillationInput>();
            await foreach (var line in File.ReadLinesAsync(input, ct))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var owner  = root.GetProperty("owner").GetString() ?? "";
                    var repo   = root.GetProperty("repo").GetString() ?? "";
                    var number = root.GetProperty("number").GetInt32();
                    var type   = root.GetProperty("type").GetString() ?? "pr";
                    records.Add(new DistillationInput(
                        Id:        $"{owner}/{repo}#{number}:{type}",
                        Title:     root.GetProperty("title").GetString() ?? "",
                        Body:      root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                        Source:    root.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                        Reactions: root.TryGetProperty("reactions", out var r) ? r.GetInt32() : 0
                    ));
                }
                catch { /* skip malformed lines */ }
            }

            Console.WriteLine($"[llm] Loaded {records.Count} records, distilling top {max}…");

            using var store    = new VectorStore(db);
            using var llm      = new RemoteLlmEngine($"{url.TrimEnd('/')}/v1/chat/completions", model, "ollama");
            using var embedEng = new OllamaEmbeddingEngine(embedM, url);

            var distillery = new Distillery(llm, embedEng, store);
            var stored     = await distillery.DistillAsync(records, max, ct);

            Console.WriteLine($"[llm] Distilled and stored {stored} expert facts → {db}");
        });

        return cmd;
    }
}
