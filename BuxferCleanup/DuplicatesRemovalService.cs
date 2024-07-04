namespace BuxferCleanup;
internal class DuplicatesRemovalService(BuxferClient buxferClient)
{
    public async Task RemoveDuplicates(string targetAccountId, string masterAccountId)
    {
        var mainTransactions = await LoadAccountTransactions(masterAccountId);
        var duplicateTransactions = await LoadAccountTransactions(targetAccountId);
        var deleted = 0;
        var failed = 0;

        foreach (var duplicateTransaction in duplicateTransactions)
        {
            if (mainTransactions.TryGetValue(duplicateTransaction.Key, out var mainTransaction))
            {
                Console.WriteLine($"Deleting transaction");
                Console.WriteLine($"Main     : {mainTransaction}");
                Console.WriteLine($"Duplicate: {duplicateTransaction.Value}");
                Console.WriteLine("==============");
                try
                {
                    await buxferClient.DeleteTransactionAsync(duplicateTransaction.Value.Id.ToString());
                    Console.WriteLine($"Deleted {duplicateTransaction.Value.Id}");
                    deleted++;
                }
                catch
                {
                    Console.WriteLine($"Failed to delete {duplicateTransaction.Value.Id}");
                    failed++;
                }
            }
        }

        Console.WriteLine($"Deleted {deleted}");
        Console.WriteLine($"Failed {failed}");

        async Task<Dictionary<string, BuxferTransaction>> LoadAccountTransactions(string accountId)
        {
            var dictionary = new Dictionary<string, BuxferTransaction>();
            await foreach (var transaction in buxferClient.LoadAllTransactionsAsync(accountId))
            {
                dictionary[transaction.DuplicationKey] = transaction;
            }

            return dictionary;
        }
    }
}
