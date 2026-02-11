using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SearchEngine.Domain.Enums;
using SearchEngine.Infrastructure.Providers;

namespace SearchEngine.UnitTests.Providers;

public class JsonContentProviderTests
{
    private const string SampleJson = """
    {
      "contents": [
        {
          "id": "v1",
          "title": "Go Programming Tutorial",
          "type": "video",
          "metrics": { "views": 15000, "likes": 1200, "duration": "15:30" },
          "published_at": "2024-03-15T10:00:00Z",
          "tags": ["programming", "tutorial"]
        }
      ],
      "pagination": { "total": 150, "page": 1, "per_page": 10 }
    }
    """;

    private static JsonContentProvider CreateProvider(HttpResponseMessage response)
    {
        var handler = new MockHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Provider1_JSON")).Returns(httpClient);

        var logger = new Mock<ILogger<JsonContentProvider>>();
        return new JsonContentProvider(factory.Object, logger.Object);
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldDeserializeJsonCorrectly()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson, Encoding.UTF8, "application/json")
        };

        var provider = CreateProvider(response);
        var contents = await provider.FetchContentsAsync();

        contents.Should().HaveCount(1);
        contents[0].Title.Should().Be("Go Programming Tutorial");
        contents[0].ExternalId.Should().Be("v1");
        contents[0].ContentType.Should().Be(ContentType.Video);
        contents[0].Views.Should().Be(15000);
        contents[0].Likes.Should().Be(1200);
        contents[0].Duration.Should().Be("15:30");
        contents[0].Tags.Should().BeEquivalentTo(["programming", "tutorial"]);
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldSetProviderName()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson, Encoding.UTF8, "application/json")
        };

        var provider = CreateProvider(response);
        provider.ProviderName.Should().Be("Provider1_JSON");

        var contents = await provider.FetchContentsAsync();
        contents[0].SourceProvider.Should().Be("Provider1_JSON");
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldThrowOnHttpError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var provider = CreateProvider(response);

        var act = () => provider.FetchContentsAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldGenerateDeterministicGuids()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson, Encoding.UTF8, "application/json")
        };

        var provider = CreateProvider(response);
        var contents1 = await provider.FetchContentsAsync();

        response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleJson, Encoding.UTF8, "application/json")
        };
        provider = CreateProvider(response);
        var contents2 = await provider.FetchContentsAsync();

        contents1[0].Id.Should().Be(contents2[0].Id);
    }
}

/// <summary>HttpMessageHandler için yardımcı mock sınıfı.</summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public MockHttpMessageHandler(HttpResponseMessage response) => _response = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_response);
}
