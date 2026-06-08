// SPDX-License-Identifier: Elastic-2.0
using System.Net.Http;
using System.Reflection;
using GauntletCI.Core;

namespace GauntletCI.Tests;

public class HttpClientFactoryTests
{
    [Fact]
    public void GetWebhookClient_DisablesAutoRedirect()
    {
        var client = HttpClientFactory.GetWebhookClient();
        var handler = FindSocketsHandler(GetRootHandler(client));

        Assert.NotNull(handler);
        Assert.False(handler!.AllowAutoRedirect);
    }

    [Fact]
    public void GetWebhookClient_ReturnsSameInstanceOnRepeatCalls()
    {
        Assert.Same(HttpClientFactory.GetWebhookClient(), HttpClientFactory.GetWebhookClient());
    }

    [Fact]
    public void GetWebhookClient_IsDistinctFromAnthropicAndGenericClients()
    {
        Assert.NotSame(HttpClientFactory.GetWebhookClient(), HttpClientFactory.GetAnthropicClient());
        Assert.NotSame(HttpClientFactory.GetWebhookClient(), HttpClientFactory.GetGenericClient());
    }

    [Fact]
    public void GetWebhookClient_UsesThirtySecondTimeout()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), HttpClientFactory.GetWebhookClient().Timeout);
    }

    private static HttpMessageHandler GetRootHandler(HttpClient client)
    {
        var field = typeof(HttpMessageInvoker).GetField(
            "_handler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (HttpMessageHandler)field!.GetValue(client)!;
    }

    private static SocketsHttpHandler? FindSocketsHandler(HttpMessageHandler handler)
    {
        if (handler is SocketsHttpHandler sockets)
            return sockets;

        if (handler is DelegatingHandler delegating && delegating.InnerHandler is not null)
            return FindSocketsHandler(delegating.InnerHandler);

        return null;
    }
}
