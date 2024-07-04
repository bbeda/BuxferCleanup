using BuxferCleanup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration(c => c.AddUserSecrets<BuxferClient>())
    .ConfigureServices((hostContext, services) =>
    {
        _ = services.AddHttpClient(BuxferClient.BuxferClientName, client =>
        {
            client.BaseAddress = new Uri("https://www.buxfer.com/api/");
        });
        _ = services.AddMemoryCache();
        _ = services.Configure<BuxferOptions>(hostContext.Configuration);
        _ = services.Configure<AccountParams>(hostContext.Configuration.GetSection("AccountParams"));

        _ = services.AddSingleton<BuxferClient>();
        _ = services.AddScoped<DuplicatesRemovalService>();
    })
    .UseConsoleLifetime()
    .Build();

using var scope = host.Services.CreateScope();

await RemoveDuplicatesAsync(scope);

static async Task RemoveDuplicatesAsync(IServiceScope scope)
{
    var removalService = scope.ServiceProvider.GetRequiredService<DuplicatesRemovalService>();
    var accountParams = scope.ServiceProvider.GetRequiredService<AccountParams>();
    await removalService.RemoveDuplicates(accountParams.DuplicateAccountId, accountParams.MainAccountId);
}

internal record AccountParams
{
    public string MainAccountId { get; init; } = string.Empty;
    public string DuplicateAccountId { get; init; } = string.Empty;
}
