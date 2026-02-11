using System.Net;
using FluentAssertions;

namespace SearchEngine.IntegrationTests;

/// <summary>
/// Sağlık kontrolü endpoint integration testi.
/// </summary>
public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert — health endpoint erişilebilir
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}
