using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Prismon.Api.Interface;

public class PythPriceFeedService : IPythPriceFeedService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythPriceFeedService> _logger;
    private readonly string _hermesBaseUrl = "https://hermes.pyth.network/v2";
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _streamSessions;
    private readonly JsonSerializerOptions _jsonOptions;

    public PythPriceFeedService(HttpClient httpClient, ILogger<PythPriceFeedService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _streamSessions = new ConcurrentDictionary<string, CancellationTokenSource>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<List<PriceFeedInfo>> GetPriceFeedsAsync(PriceFeedRequest request)
    {
        try
        {
            // Build query string
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(request.Query))
                queryParams.Add($"query={Uri.EscapeDataString(request.Query)}");
            if (!string.IsNullOrEmpty(request.AssetType))
                queryParams.Add($"asset_type={Uri.EscapeDataString(request.AssetType)}");
            var queryString = queryParams.Any() ? $"?{string.Join("&", queryParams)}" : "";
            var requestUrl = $"{_hermesBaseUrl}/price_feeds{queryString}";

            _logger.LogDebug("Fetching price feeds from {Url}", requestUrl);

            // Send HTTP request
            var response = await _httpClient.GetAsync(requestUrl);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Raw Hermes API response: {ResponseBody}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Hermes API returned {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                throw new HttpRequestException($"Hermes API returned {response.StatusCode}: {responseBody}");
            }

            // Parse response
            var feeds = JsonSerializer.Deserialize<List<PriceFeedInfo>>(responseBody, _jsonOptions);
            if (feeds == null || feeds.Count == 0)
            {
                _logger.LogWarning("Hermes API returned empty or null price feeds list");
                return new List<PriceFeedInfo>();
            }

            _logger.LogInformation("Retrieved {FeedCount} price feeds", feeds.Count);
            return feeds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price feeds");
            throw;
        }
    }

    public async Task<List<PriceUpdate>> GetLatestPriceAsync(LatestPriceRequest request)
    {
        try
        {
            // Validate inputs
            if (request.PriceFeedIds.Count == 0)
                throw new ArgumentException("At least one price feed ID is required");

            // Build query string
            var queryParams = new List<string>();
            queryParams.AddRange(request.PriceFeedIds.Select(id => $"ids[]={Uri.EscapeDataString(id)}"));
            if (request.IgnoreInvalidPriceIds)
                queryParams.Add("ignore_invalid_price_ids=true");
            var queryString = $"?{string.Join("&", queryParams)}";
            var requestUrl = $"{_hermesBaseUrl}/updates/price/latest{queryString}";

            _logger.LogDebug("Fetching latest prices from {Url}", requestUrl);

            // Send HTTP request
            var response = await _httpClient.GetAsync(requestUrl);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Raw Hermes API response: {ResponseBody}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Hermes API returned {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                throw new HttpRequestException($"Hermes API returned {response.StatusCode}: {responseBody}");
            }

            // Parse response
            var priceResponse = JsonSerializer.Deserialize<PriceUpdateResponse>(responseBody, _jsonOptions)
                ?? throw new Exception("Invalid response from Hermes API");

            _logger.LogInformation("Retrieved latest prices for {FeedCount} feeds", priceResponse.Parsed.Count);
            return priceResponse.Parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching latest prices");
            throw;
        }
    }

    public async Task StartPriceStreamAsync(string sessionId, StreamPriceRequest request, Func<PriceUpdate, Task> onPriceUpdate)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(sessionId) || request.PriceFeedIds.Count == 0 || onPriceUpdate == null)
                throw new ArgumentException("Invalid input parameters");

            // Check if stream already exists
            if (_streamSessions.ContainsKey(sessionId))
            {
                _logger.LogWarning("Stream already active for session {SessionId}, stopping existing stream", sessionId);
                await StopPriceStreamAsync(sessionId);
            }

            // Build query string
            var queryParams = new List<string>();
            queryParams.AddRange(request.PriceFeedIds.Select(id => $"ids[]={Uri.EscapeDataString(id)}"));
            queryParams.Add($"encoding={Uri.EscapeDataString(request.Encoding)}");
            queryParams.Add($"parsed={request.Parsed.ToString().ToLower()}");
            if (request.AllowUnordered)
                queryParams.Add("allow_unordered=true");
            if (request.BenchmarksOnly)
                queryParams.Add("benchmarks_only=true");
            if (request.IgnoreInvalidPriceIds)
                queryParams.Add("ignore_invalid_price_ids=true");
            var queryString = $"?{string.Join("&", queryParams)}";
            var requestUrl = $"{_hermesBaseUrl}/updates/price/stream{queryString}";

            _logger.LogDebug("Starting price stream from {Url}", requestUrl);

            // Initialize streaming
            var cts = new CancellationTokenSource();
            _streamSessions[sessionId] = cts;

            // Stream response
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await _httpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Hermes API returned {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
                        throw new HttpRequestException($"Hermes API returned {response.StatusCode}: {responseBody}");
                    }

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync();
                        if (!string.IsNullOrEmpty(line))
                        {
                            _logger.LogDebug("Raw stream line: {Line}", line);
                            var priceResponse = JsonSerializer.Deserialize<PriceUpdateResponse>(line, _jsonOptions);
                            if (priceResponse?.Parsed != null)
                            {
                                foreach (var priceUpdate in priceResponse.Parsed)
                                {
                                    _logger.LogDebug("Received price update for {FeedId}: {Price}", priceUpdate.Id, priceUpdate.Price.Price);
                                    await onPriceUpdate(priceUpdate);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in price stream for session {SessionId}", sessionId);
                }
                finally
                {
                    await StopPriceStreamAsync(sessionId);
                }
            }, cts.Token);

            _logger.LogInformation("Price streaming started for session {SessionId}, feeds: {FeedCount}", sessionId, request.PriceFeedIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting price stream for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task StopPriceStreamAsync(string sessionId)
    {
        try
        {
            if (_streamSessions.TryRemove(sessionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _logger.LogInformation("Stopped price stream for session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping price stream for session {SessionId}", sessionId);
            throw;
        }
    }
}

public class PriceUpdateResponse
{
    [JsonPropertyName("binary")]
    public BinaryData Binary { get; set; } = new BinaryData();

    [JsonPropertyName("parsed")]
    public List<PriceUpdate> Parsed { get; set; } = new List<PriceUpdate>();
}

public class BinaryData
{
    [JsonPropertyName("data")]
    public List<string> Data { get; set; } = new List<string>();

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = string.Empty;
}


public class PriceFeedInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public PriceFeedAttributes Attributes { get; set; } = new PriceFeedAttributes();
}

public class PriceFeedAttributes
{
    [JsonPropertyName("asset_type")]
    public string AssetType { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string Base { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("display_symbol")]
    public string DisplaySymbol { get; set; } = string.Empty;

    [JsonPropertyName("generic_symbol")]
    public string GenericSymbol { get; set; } = string.Empty;

    [JsonPropertyName("quote_currency")]
    public string QuoteCurrency { get; set; } = string.Empty;

    [JsonPropertyName("schedule")]
    public string Schedule { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
}

public class PriceUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public PriceData Price { get; set; } = new PriceData();

    [JsonPropertyName("ema_price")]
    public PriceData EmaPrice { get; set; } = new PriceData();

    [JsonPropertyName("metadata")]
    public PriceMetadata Metadata { get; set; } = new PriceMetadata();
}

public class PriceData
{
    [JsonPropertyName("conf")]
    public string Conf { get; set; } = string.Empty;

    [JsonPropertyName("expo")]
    public int Expo { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("publish_time")]
    public long PublishTime { get; set; }
}

public class PriceMetadata
{
    [JsonPropertyName("prev_publish_time")]
    public long PrevPublishTime { get; set; }

    [JsonPropertyName("proof_available_time")]
    public long ProofAvailableTime { get; set; }

    [JsonPropertyName("slot")]
    public long Slot { get; set; }
}

public class PriceFeedRequest
{
    public string? Query { get; set; }
    public string? AssetType { get; set; }
}

public class LatestPriceRequest
{
    public List<string> PriceFeedIds { get; set; } = new List<string>();
    public bool IgnoreInvalidPriceIds { get; set; } = true;
}

public class StreamPriceRequest
{
    public List<string> PriceFeedIds { get; set; } = new List<string>();
    public string Encoding { get; set; } = "hex";
    public bool Parsed { get; set; } = true;
    public bool AllowUnordered { get; set; } = false;
    public bool BenchmarksOnly { get; set; } = false;
    public bool IgnoreInvalidPriceIds { get; set; } = true;
}
