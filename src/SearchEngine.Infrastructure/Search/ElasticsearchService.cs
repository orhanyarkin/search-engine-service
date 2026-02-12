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
        // E-ticaret tarzı arama: prefix + fuzzy + wildcard kombinasyonu
        // Her keyword uzunluğunda sonuç döner, en alakalı olanlar üstte sıralanır
        var lowerKeyword = keyword.ToLowerInvariant();
        var keywordQuery = new BoolQuery
        {
            Should = new Query[]
            {
                // 1) Title prefix — "go" → "Go Programming...", "adv" → "Advanced..."
                new MatchPhrasePrefixQuery("title"!) { Query = keyword, Boost = 5 },
                // 2) Tags prefix — "dev" → "devops", "prog" → "programming"
                new MatchPhrasePrefixQuery("tags"!) { Query = keyword, Boost = 3 },
                // 3) Fuzzy multi-match — yazım hataları ve tam eşleşme
                new MultiMatchQuery
                {
                    Query = keyword,
                    Fields = new[] { "title^3", "tags^2" },
                    Fuzziness = new Fuzziness("AUTO"),
                    Type = TextQueryType.BestFields
                },
                // 4) Title wildcard — substring arama garantisi
                new WildcardQuery("title"!) { Value = $"*{lowerKeyword}*", Boost = 0.5f },
                // 5) Tags wildcard — "dev" → "devops", "ci" → "ci-cd"
                new WildcardQuery("tags"!) { Value = $"*{lowerKeyword}*", Boost = 0.5f }
            },
            MinimumShouldMatch = 1
        };

        var mustQueries = new List<Query> { keywordQuery };

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
            _logger.LogWarning("Elasticsearch araması başarısız: {Reason}", searchResponse.DebugInformation);
            throw new InvalidOperationException(
                $"Elasticsearch araması başarısız: {searchResponse.DebugInformation}");
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
            if (!pingResponse.IsValidResponse)
                return false;

            // Sadece ping değil, index varlığını da kontrol et
            var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);
            return existsResponse.Exists;
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
                    .Text(t => t.Tags, td => td.Analyzer("standard"))
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
