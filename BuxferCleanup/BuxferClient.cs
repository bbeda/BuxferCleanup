using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;

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

    public async Task<TransactionsListResponse> LoadTransactionsAsync(string accountId, int page = 1)
    {
        var token = await LoadTokenAsync();
        using var client = CreateHttpClient();
        var response = await client.GetAsync($"transactions?token={token}&page={page}&accountId={accountId}");
        return (await response!.Content!.ReadFromJsonAsync<BuxferResponse<TransactionsListResponse>>())?.Response!;
    }

    public async IAsyncEnumerable<BuxferTransaction> LoadAllTransactionsAsync(string accountId)
    {
        var page = 1;
        TransactionsListResponse? response;
        do
        {
            response = await LoadTransactionsAsync(accountId, page++);
            foreach (var transaction in response.Transactions)
            {
                yield return transaction;
            }

            if (response.Transactions.Length == 0)
            {
                break;
            }
        } while (true);
    }

    public async Task DeleteTransactionAsync(string transactionId)
    {
        using var client = CreateHttpClient();
        var token = await LoadTokenAsync();
        var response = await client.PostAsync($"delete_transaction?token={token}&id={transactionId}", new StringContent(""));
        response.EnsureSuccessStatusCode();
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
    string Tags)
{
    public string DuplicationKey
    {
        get
        {
            using var memoryStream = new MemoryStream();
            using var binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(Description);
            binaryWriter.Write(Amount);
            binaryWriter.Write(Date);

            return Convert.ToBase64String(memoryStream.ToArray());
        }
    }
}

internal record TransactionsListResponse(
    string Status,
    int NumTransactions,
    BuxferTransaction[] Transactions);
