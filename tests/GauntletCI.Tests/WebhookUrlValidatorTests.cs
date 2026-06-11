// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Security;

namespace GauntletCI.Tests;

public class WebhookUrlValidatorTests
{
    [Theory]
    [InlineData("https://hooks.slack.com/services/T000/B000/XXXXXXXX")]
    [InlineData("https://hooks.slack.com/triggers/E000/000/000")]
    public void TryValidateSlack_AcceptsOfficialHosts(string url)
    {
        Assert.True(WebhookUrlValidator.TryValidateSlack(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("http://hooks.slack.com/services/T000/B000/XXXXXXXX")]
    [InlineData("https://evil.com/hooks.slack.com/services/T000/B000/XXXXXXXX")]
    [InlineData("https://hooks.slack.com.evil.com/services/T000/B000/XXXXXXXX")]
    [InlineData("https://127.0.0.1/webhook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    public void TryValidateSlack_RejectsUnsafeUrls(string url)
    {
        Assert.False(WebhookUrlValidator.TryValidateSlack(url, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("https://contoso.webhook.office.com/webhookb2/guid/IncomingWebhook/token/token")]
    [InlineData("https://prod-12.westus.logic.azure.com:443/workflows/abc/triggers/manual/paths/invoke")]
    [InlineData("https://outlook.office.com/webhook/guid@guid/IncomingWebhook/token/token")]
    public void TryValidateTeams_AcceptsOfficialHosts(string url)
    {
        Assert.True(WebhookUrlValidator.TryValidateTeams(url, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("https://outlook.office.com/not-a-webhook")]
    [InlineData("https://evil.com/webhook")]
    [InlineData("http://contoso.webhook.office.com/webhookb2/guid")]
    public void TryValidateTeams_RejectsUnsafeUrls(string url)
    {
        Assert.False(WebhookUrlValidator.TryValidateTeams(url, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
