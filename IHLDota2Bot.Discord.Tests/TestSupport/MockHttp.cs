using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Moq.Protected;

namespace IHLDota2Bot.Discord.Tests.TestSupport;

/// <summary>
/// Wires a Moq <see cref="IHttpClientFactory"/> to a Moq <see cref="HttpMessageHandler"/>
/// so the <c>SteamClient</c> HTTP wrapper can be tested without a real server: the
/// handler returns a scripted response and records the outgoing request, letting
/// tests assert the exact endpoint/verb the bot calls and how it reacts to status
/// codes and transport failures.
/// </summary>
internal sealed class MockHttp
{
    public List<(HttpMethod Method, Uri Uri)> Requests { get; } = [];
    public Mock<IHttpClientFactory> Factory { get; } = new();

    private MockHttp() { }

    /// <param name="responder">
    /// Produces the response for each request. Throw inside it to simulate a
    /// transport failure (DNS, connection reset, timeout).
    /// </param>
    public static MockHttp Returning(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var mock = new MockHttp();

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns(
                (HttpRequestMessage req, CancellationToken _) =>
                {
                    mock.Requests.Add((req.Method, req.RequestUri!));
                    return Task.FromResult(responder(req));
                }
            );

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost/") };
        mock.Factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return mock;
    }

    /// <summary>Always returns the given body as JSON with HTTP 200.</summary>
    public static MockHttp ReturningJson<T>(T body) => Returning(_ => JsonOk(body));

    /// <summary>Always returns the given status code with no body.</summary>
    public static MockHttp ReturningStatus(HttpStatusCode code) =>
        Returning(_ => new HttpResponseMessage(code));

    /// <summary>Always throws, simulating a transport failure.</summary>
    public static MockHttp ThrowingTransportError() =>
        Returning(_ => throw new HttpRequestException("simulated connection failure"));

    public static HttpResponseMessage JsonOk<T>(T body) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body, options: JsonSerializerOptions.Web),
        };

    /// <summary>The single request that was sent (fails if zero or more than one).</summary>
    public (HttpMethod Method, Uri Uri) SingleRequest() => Assert.Single(Requests);
}
