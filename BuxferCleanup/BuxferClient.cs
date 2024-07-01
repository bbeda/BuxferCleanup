using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace BuxferCleanup;
internal class BuxferClient(
    IHttpClientFactory httpClientFactory,
    IOptions<BuxferOptions> buxferOptions,
    IMemoryCache memoryCache)
{
    private const string BuxferTokenKey = "BuxferToken";

    public const string BuxferClientName = "Buxfer";

    private async Task<string> GetTokenAsync()
    {
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"login?email={buxferOptions.Value.Email}&password={buxferOptions.Value.Password}");
        _ = response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<BuxferResponse<TokenResponse>>();
        return tokenResponse?.Response.Token!;
    }

    private async Task<string> LoadTokenAsync()
    {
        if (memoryCache.TryGetValue(BuxferTokenKey, out string cachedToken))
        {
            return cachedToken!;
        }

        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Failed to get token from Buxfer");
        }

        memoryCache.Set(BuxferTokenKey, token, TimeSpan.FromHours(5));

        return token;
    }

    public async Task<TransactionsListResponse> LoadTransactionsAsync(string accountId)
    {
        var token = await LoadTokenAsync();
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"transactions?token={token}&page=1&accountId={accountId}");
        return (await response!.Content!.ReadFromJsonAsync<BuxferResponse<TransactionsListResponse>>())?.Response!;
    }

    private HttpClient CreateHttpClient() => httpClientFactory.CreateClient(BuxferClientName);

    private record TokenResponse(string Status, string Token);

    private record BuxferResponse<T>
    {
        public T Response { get; init; } = default!;
    }
}

internal record BuxferOptions
{
    public string Email { get; set; }

    public string Password { get; set; }
}

internal record BuxferTransaction(
    decimal Id,
    string Description,
    decimal Amount,
    string Date,
    string Type,
    decimal AccountId,
    string Tags);

internal record TransactionsListResponse(
    string Status,
    int NumTransactions,
    BuxferTransaction[] Transactions);
