using System.Security.Cryptography;
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

	string? webhookSecret = Environment.GetEnvironmentVariable("GITHUB_WEBHOOK_SECRET");

	using StreamReader reader = new(request.Body, Encoding.UTF8);
	string payload = await reader.ReadToEndAsync(cancellationToken);

	if (!string.IsNullOrWhiteSpace(webhookSecret))
	{
		string? signatureHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
		if (!IsValidSignature(payload, webhookSecret, signatureHeader))
		{
			return Results.Json(new { error = "Invalid webhook signature." }, statusCode: 401);
		}
	}

	string workingDirectory = environment.ContentRootPath;

	PrIntegrationHost host = new();
	PrEvaluationSummary summary = await host.ProcessWebhookAsync(payload, workingDirectory, cancellationToken);
	return Results.Ok(summary);
});

app.Run();

static bool IsValidSignature(string payload, string secret, string? signatureHeader)
{
	if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
	{
		return false;
	}

	byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
	byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
	byte[] hash = HMACSHA256.HashData(keyBytes, payloadBytes);
	string expectedSignature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

	return CryptographicOperations.FixedTimeEquals(
		Encoding.ASCII.GetBytes(expectedSignature),
		Encoding.ASCII.GetBytes(signatureHeader));
}
