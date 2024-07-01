using BuxferCleanup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder().ConfigureAppConfiguration(c => c.AddUserSecrets<BuxferClient>()).ConfigureServices((hostContext, services) =>
{
    _ = services.AddHttpClient(BuxferClient.BuxferClientName, client =>
    {
        client.BaseAddress = new Uri("https://www.buxfer.com/api/");
    });
    _ = services.AddMemoryCache();
    _ = services.Configure<BuxferOptions>(hostContext.Configuration);

    _ = services.AddSingleton<BuxferClient>();
}).UseConsoleLifetime()
.Build();

var buxferClient = host.Services.GetRequiredService<BuxferClient>();

var transactions = await buxferClient.LoadTransactionsAsync("1441844");

foreach (var transaction in transactions.Transactions)
{
    Console.WriteLine($"{transaction.Id} - {transaction.AccountId} - {transaction.Date} - {transaction.Amount} - {transaction.Description}");
}
