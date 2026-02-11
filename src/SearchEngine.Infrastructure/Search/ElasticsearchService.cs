using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using SearchEngine.Domain.Entities;
using SearchEngine.Domain.Enums;
using SearchEngine.Domain.Interfaces;

namespace SearchEngine.Infrastructure.Search;

/// <summary>
/// Tam metin arama islemleri icin Elasticsearch uygulamasi.
/// </summary>
public class ElasticsearchService : ISearchService
{
    private const string IndexName = "searchengine-contents";
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ElasticsearchService> _logger;

    public ElasticsearchService(ElasticsearchClient client, ILogger<ElasticsearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task IndexManyAsync(List<Content> contents, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureIndexAsync(cancellationToken);

            var bulkResponse = await _client.BulkAsync(b => b
                .Index(IndexName)
                .IndexMany(contents, (descriptor, content) => descriptor.Id(content.Id.ToString())),
                cancellationToken);

            if (bulkResponse.Errors)
            {
                var errorItems = bulkResponse.ItemsWithErrors.Select(i => i.Error?.Reason);
                _logger.LogWarning("Some items failed to index: {Errors}", string.Join("; ", errorItems));
            }
            else
            {
                _logger.LogInformation("Successfully indexed {Count} items in Elasticsearch.", contents.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index {Count} items in Elasticsearch. Search will fall back to database.", contents.Count);
        }
    }

    /// <inheritdoc />
    public async Task<(List<Content> Items, int TotalCount)> SearchAsync(
        string keyword,
        ContentType? type,
        SortBy sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var mustQueries = new List<Query>
        {
            new MultiMatchQuery
            {
                Query = keyword,
                Fields = new[] { "title^3", "tags" },
                Fuzziness = new Fuzziness("AUTO"),
                Type = TextQueryType.BestFields
            }
        };

        if (type.HasValue)
        {
            mustQueries.Add(new TermQuery("contentType"!) { Value = type.Value.ToString() });
        }

        var searchResponse = await _client.SearchAsync<Content>(s => s
            .Index(IndexName)
            .From((page - 1) * pageSize)
            .Size(pageSize)
            .Query(q => q.Bool(b => b.Must(mustQueries.ToArray())))
            .Sort(BuildSort(sortBy)),
            cancellationToken);

        if (!searchResponse.IsValidResponse)
        {
            _logger.LogWarning("Elasticsearch search failed: {Reason}", searchResponse.DebugInformation);
            return ([], 0);
        }

        var totalCount = (int)searchResponse.Total;
        var items = searchResponse.Documents.ToList();

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pingResponse = await _client.PingAsync(cancellationToken);
            return pingResponse.IsValidResponse;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);
        if (existsResponse.Exists)
            return;

        var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Mappings(m => m
                .Properties<Content>(p => p
                    .Keyword(k => k.Id!)
                    .Text(t => t.Title, td => td.Analyzer("standard"))
                    .Keyword(k => k.ExternalId)
                    .Keyword(k => k.SourceProvider)
                    .Keyword(k => k.ContentType)
                    .IntegerNumber(n => n.Views!)
                    .IntegerNumber(n => n.Likes!)
                    .Keyword(k => k.Duration!)
                    .IntegerNumber(n => n.ReadingTime!)
                    .IntegerNumber(n => n.Reactions!)
                    .IntegerNumber(n => n.Comments!)
                    .Date(d => d.PublishedAt)
                    .Keyword(k => k.Tags)
                    .FloatNumber(f => f.FinalScore)
                    .Date(d => d.LastSyncedAt)
                )),
            cancellationToken);

        if (createResponse.IsValidResponse)
            _logger.LogInformation("Created Elasticsearch index '{Index}'.", IndexName);
        else
            _logger.LogWarning("Failed to create Elasticsearch index: {Reason}", createResponse.DebugInformation);
    }

    private static Action<SortOptionsDescriptor<Content>> BuildSort(SortBy sortBy)
    {
        return sortBy switch
        {
            SortBy.Popularity => s => s.Field(f => f.FinalScore, new FieldSort { Order = SortOrder.Desc }),
            SortBy.Recency => s => s.Field(f => f.PublishedAt, new FieldSort { Order = SortOrder.Desc }),
            SortBy.Relevance => s => s.Score(new ScoreSort { Order = SortOrder.Desc }),
            _ => s => s.Field(f => f.FinalScore, new FieldSort { Order = SortOrder.Desc })
        };
    }
}
