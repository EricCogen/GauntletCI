using System.Text;
using GauntletCI.PrIntegration;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/github/webhooks/pull_request", async (HttpRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
{
	string? eventName = request.Headers["X-GitHub-Event"].FirstOrDefault();
	if (!string.Equals(eventName, "pull_request", StringComparison.OrdinalIgnoreCase))
	{
		return Results.BadRequest(new { error = "Unsupported GitHub event." });
	}

	using StreamReader reader = new(request.Body, Encoding.UTF8);
	string payload = await reader.ReadToEndAsync(cancellationToken);
	string workingDirectory = environment.ContentRootPath;

	PrIntegrationHost host = new();
	PrEvaluationSummary summary = await host.ProcessWebhookAsync(payload, workingDirectory, cancellationToken);
	return Results.Ok(summary);
});

app.Run();
