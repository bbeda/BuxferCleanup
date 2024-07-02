using BuxferCleanup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder().ConfigureAppConfiguration(c => c.AddUserSecrets<BuxferClient>()).ConfigureServices((hostContext, services) =>
{
    _ = services.AddHttpClient(BuxferClient.BuxferClientName, client =>
    {
        client.BaseAddress = new Uri("https://www.buxfer.com/api/");
    });
    _ = services.AddMemoryCache();
    _ = services.Configure<BuxferOptions>(hostContext.Configuration);
    services.Configure<AccountParams>(hostContext.Configuration.GetSection("AccountParams"));

    _ = services.AddSingleton<BuxferClient>();
}).UseConsoleLifetime()
.Build();

var buxferClient = host.Services.GetRequiredService<BuxferClient>();

var accountParamsOptions = host.Services.GetRequiredService<IOptions<AccountParams>>();

var mainTransactions = await LoadAccountTransactions(accountParamsOptions.Value.MainAccountId);
var duplicateTransactions = await LoadAccountTransactions(accountParamsOptions.Value.DuplicateAccountId);
var toDelete = new List<BuxferTransaction>();

foreach (var duplicateTransaction in duplicateTransactions)
{
    if (mainTransactions.TryGetValue(duplicateTransaction.Key, out var mainTransaction))
    {
        Console.WriteLine($"Deleting transaction");
        Console.WriteLine($"Main     : {mainTransaction}");
        Console.WriteLine($"Duplicate: {duplicateTransaction.Value}");
        Console.WriteLine("==============");
        toDelete.Add(duplicateTransaction.Value);
    }
}

Console.WriteLine($"To Delete {toDelete.Count}");

async Task<Dictionary<string, BuxferTransaction>> LoadAccountTransactions(string accountId)
{
    var dictionary = new Dictionary<string, BuxferTransaction>();
    await foreach (var transaction in buxferClient.LoadAllTransactionsAsync(accountId))
    {
        dictionary[transaction.DuplicationKey] = transaction;
    }

    return dictionary;
}

record AccountParams
{
    public string MainAccountId { get; init; } = string.Empty;
    public string DuplicateAccountId { get; init; } = string.Empty;
}
