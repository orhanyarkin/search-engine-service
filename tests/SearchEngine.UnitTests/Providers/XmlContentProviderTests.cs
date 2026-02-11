using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SearchEngine.Domain.Enums;
using SearchEngine.Infrastructure.Providers;

namespace SearchEngine.UnitTests.Providers;

public class XmlContentProviderTests
{
    private const string SampleXml = """
    <?xml version="1.0" encoding="UTF-8"?>
    <feed>
      <items>
        <item>
          <id>v1</id>
          <headline>Introduction to Docker</headline>
          <type>video</type>
          <stats>
            <views>22000</views>
            <likes>1800</likes>
            <duration>25:15</duration>
          </stats>
          <publication_date>2024-03-15</publication_date>
          <categories><category>devops</category><category>containers</category></categories>
        </item>
        <item>
          <id>a1</id>
          <headline>Clean Architecture in Go</headline>
          <type>article</type>
          <stats>
            <reading_time>8</reading_time>
            <reactions>450</reactions>
            <comments>25</comments>
          </stats>
          <publication_date>2024-03-14</publication_date>
          <categories><category>programming</category><category>architecture</category></categories>
        </item>
      </items>
      <meta>
        <total_count>75</total_count>
        <current_page>1</current_page>
        <items_per_page>10</items_per_page>
      </meta>
    </feed>
    """;

    private static XmlContentProvider CreateProvider(HttpResponseMessage response)
    {
        var handler = new MockHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Provider2_XML")).Returns(httpClient);

        var logger = new Mock<ILogger<XmlContentProvider>>();
        return new XmlContentProvider(factory.Object, logger.Object);
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldDeserializeVideoCorrectly()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleXml, Encoding.UTF8, "application/xml")
        };

        var provider = CreateProvider(response);
        var contents = await provider.FetchContentsAsync();

        contents.Should().HaveCount(2);

        var video = contents.First(c => c.ExternalId == "v1");
        video.Title.Should().Be("Introduction to Docker");
        video.ContentType.Should().Be(ContentType.Video);
        video.Views.Should().Be(22000);
        video.Likes.Should().Be(1800);
        video.Duration.Should().Be("25:15");
        video.Tags.Should().BeEquivalentTo(["devops", "containers"]);
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldDeserializeArticleCorrectly()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleXml, Encoding.UTF8, "application/xml")
        };

        var provider = CreateProvider(response);
        var contents = await provider.FetchContentsAsync();

        var article = contents.First(c => c.ExternalId == "a1");
        article.Title.Should().Be("Clean Architecture in Go");
        article.ContentType.Should().Be(ContentType.Article);
        article.ReadingTime.Should().Be(8);
        article.Reactions.Should().Be(450);
        article.Comments.Should().Be(25);
        article.Tags.Should().BeEquivalentTo(["programming", "architecture"]);
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldSetProviderName()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleXml, Encoding.UTF8, "application/xml")
        };

        var provider = CreateProvider(response);
        provider.ProviderName.Should().Be("Provider2_XML");

        var contents = await provider.FetchContentsAsync();
        contents.Should().OnlyContain(c => c.SourceProvider == "Provider2_XML");
    }

    [Fact]
    public async Task FetchContentsAsync_ShouldThrowOnHttpError()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var provider = CreateProvider(response);

        var act = () => provider.FetchContentsAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
